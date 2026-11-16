using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using HIDMaestro;

internal static class Program
{
    internal enum Kind { Mouse, Key, Consumer }
    internal record struct Input(Kind Kind,uint Code,uint Data,bool Down,long Tick,long Epoch,long Stamp=0,long Id=0);
    static readonly ConcurrentQueue<string> Logs=new();
    static readonly ConcurrentQueue<Input> Pending=new();
    static readonly AutoResetEvent Signal=new(false);
    static readonly object Gate=new();
    static readonly Stopwatch Clock=Stopwatch.StartNew();
    static readonly Proc MouseCallback=MouseHook,KeyCallback=KeyHook;
    static readonly KeyboardState KeysHeld=new();
    static readonly SortedSet<ushort> ConsumerHeld=new();
    static readonly MouseState MouseHeld=new();
    static long InputSequence;
    static HMContext? Context;
    static HMController? Mouse,Keyboard;
    static IntPtr MouseHookId,KeyHookId;
    static volatile bool Running=true,Logging=true;
    static int Mode,Fault,ArmCancel,Armed,IncomingModifiers,StopVk=0x13,Detail,CurrentKeys,PeakKeys,CurrentButtons;
    static int TrialSeconds;
    static long Epoch,Until,Sequence,MouseInjected,MouseHardware,KeyInjected,KeyHardware,SentMouse,SentKey,SentConsumer,Errors,Unmapped;
    static bool ReservedStop;
    static string Root=AppContext.BaseDirectory,FaultReason="requested";
    static void Log(string s)=>Logs.Enqueue($"{Clock.ElapsedMilliseconds,8} {s}");
    static void Trace(string s) {if(Volatile.Read(ref Detail)!=0)Log(s);}
    static void RequestStop(string reason)
    {
        Interlocked.Exchange(ref Mode,0);Interlocked.Increment(ref Epoch);
        FaultReason=reason;Interlocked.Exchange(ref ArmCancel,1);Interlocked.Exchange(ref Armed,0);Interlocked.Exchange(ref Fault,1);Signal.Set();
    }
    [STAThread]
    internal static void Run(string[] args)
    {
        if(args.Contains("--self-test"))
        {
            try {KeyboardMap.SelfTest();MouseState.SelfTest();Profiles.SelfTest();BridgeWindow.SelfTest();File.WriteAllText(Path.Combine(Root,"selftest.txt"),"PASS: keyboard mapping/chords/releases; NKRO profiles; five mouse buttons; independent L/R chords without middle emulation; wheel; UI activation/cancellation/emergency/reset/error states\n");}
            catch(Exception e) {File.WriteAllText(Path.Combine(Root,"selftest.txt"),e.ToString());Environment.ExitCode=1;}
            return;
        }
        // Share the session mutex with v5/v5.1 to prevent two bridges redirecting together.
        using var instance=new Mutex(true,"Local\\HIDMaestroMoonlightBridgeV5",out bool first);
        if(!first) {MessageBox.Show("Сначала закрой другое окно моста (v5/v5.1/v6). / Close the other bridge window first.");return;}
        Directory.SetCurrentDirectory(Root);
        string logDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Aion2RemotePlay","logs");
        Directory.CreateDirectory(logDir);
        string logPath=Path.Combine(logDir,"bridge-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".log");
        using var writer=new StreamWriter(logPath) {AutoFlush=true};
        var logger=new Thread(()=>{while(Logging || !Logs.IsEmpty){while(Logs.TryDequeue(out var s))writer.WriteLine(s);Thread.Sleep(10);}});
        logger.Start();Thread? sender=null,guard=null;
        try
        {
            Log($"START v6.0 pid={Environment.ProcessId} session={Process.GetCurrentProcess().SessionId} sdk={typeof(HMContext).Assembly.FullName}");
            Context=new HMContext();Context.LoadDefaultProfiles();
            Mouse=Context.CreateController(Context.GetProfile("logitech-gaming-mouse-g502")!,"moonlight-bridge-v5-mouse");
            Keyboard=Context.CreateController(Profiles.Keyboard(Context.GetProfile("microsoft-wired-keyboard-600")!),"moonlight-bridge-v5-keyboard");
            ReleaseDevices("initial");
            MouseHookId=SetWindowsHookEx(14,MouseCallback,GetModuleHandle(null),0);
            if(MouseHookId==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            KeyHookId=SetWindowsHookEx(13,KeyCallback,GetModuleHandle(null),0);
            if(KeyHookId==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            sender=new Thread(SendLoop){IsBackground=true};sender.Start();
            guard=new Thread(()=>
            {
                while(Running)
                {
                    if(Interlocked.Exchange(ref Fault,0)!=0)Stop(FaultReason);
                    if(Volatile.Read(ref Mode)!=0 && Clock.ElapsedMilliseconds>=Interlocked.Read(ref Until))Stop("timeout");
                    try
                    {
                        string p=Path.Combine(logDir,"off.request");
                        if(File.Exists(p)){string c=File.ReadAllText(p).Trim();File.Delete(p);if(c=="off"){Interlocked.Exchange(ref ArmCancel,1);Stop("remote");}}
                    }
                    catch(Exception e){Log("COMMAND ERROR "+e.Message);}
                    Thread.Sleep(20);
                }
            }){IsBackground=true};guard.Start();
            using var form=new BridgeWindow(
                (seconds,detailed,stopVk)=>
                {
                    TrialSeconds=seconds;Interlocked.Exchange(ref Detail,detailed && seconds==90?1:0);
                    Interlocked.Exchange(ref StopVk,stopVk);Interlocked.Exchange(ref ArmCancel,0);
                    return Start(3)?null:"Отпустите все клавиши и попробуйте снова. / Release all keys and try again.";
                },
                ()=>{Interlocked.Exchange(ref ArmCancel,1);Stop("button");},
                ()=>new BridgeSnapshot(Volatile.Read(ref Mode)!=0,Volatile.Read(ref Armed)!=0,
                    Volatile.Read(ref ArmCancel)!=0,Interlocked.Read(ref Errors),
                    Interlocked.Read(ref SentMouse),Interlocked.Read(ref SentKey)+Interlocked.Read(ref SentConsumer)),
                (armed,stopVk)=>{Interlocked.Exchange(ref StopVk,stopVk);if(armed)Interlocked.Exchange(ref ArmCancel,0);Interlocked.Exchange(ref Armed,armed?3:0);},
                logDir);
            Log("READY v6 default OFF; independent L/R; no pair grouping; detailed logging OFF");
            Application.Run(form);
        }
        catch(Exception e){Log("FATAL "+e);MessageBox.Show("Не удалось запустить мост. Проверьте установку HIDMaestro 2.1.0 и запуск от администратора.\nBridge startup failed. Check HIDMaestro 2.1.0 installation and administrator access.\n\n"+e.Message+"\n\nЖурнал / Log: "+logPath,"aion2-remote-play v6",MessageBoxButtons.OK,MessageBoxIcon.Error);Environment.ExitCode=1;}
        finally
        {
            if(MouseHookId!=IntPtr.Zero)UnhookWindowsHookEx(MouseHookId);
            if(KeyHookId!=IntPtr.Zero)UnhookWindowsHookEx(KeyHookId);
            Stop("shutdown");Running=false;Signal.Set();sender?.Join(2000);guard?.Join(1000);
            if(sender==null || !sender.IsAlive){Keyboard?.Dispose();Mouse?.Dispose();Context?.Dispose();}
            Log("EXIT");Logging=false;logger.Join(2000);
        }
    }
    static bool Start(int mode)
    {
        if((mode&2)!=0)
            for(int vk=8;vk<255;vk++)if((GetAsyncKeyState(vk)&0x8000)!=0)
            {Log("ARM CANCELLED: keys held");return false;}
        lock(Gate)
        {
            while(Pending.TryDequeue(out _)){}
            Interlocked.Increment(ref Epoch);KeysHeld.Clear();ConsumerHeld.Clear();MouseHeld.Clear();
            CurrentKeys=PeakKeys=CurrentButtons=0;
            Until=TrialSeconds==0?long.MaxValue:Clock.ElapsedMilliseconds+TrialSeconds*1000L;
            Mode=mode;Log($"ON mode={mode} epoch={Epoch} duration={TrialSeconds}");
        }
        return true;
    }
    static void ReleaseDevices(string reason)
    {
        KeysHeld.Clear();ConsumerHeld.Clear();MouseHeld.Clear();CurrentKeys=CurrentButtons=0;
        try{Keyboard?.SubmitKeyboard(HMKeyModifiers.None,Array.Empty<HMKey>());Log("OFF keyboard released "+reason);}catch(Exception e){Interlocked.Increment(ref Errors);Log("KEY RELEASE ERROR "+e.Message);}
        try{Keyboard?.SubmitConsumer(Array.Empty<ushort>());Log("OFF consumer released "+reason);}catch(Exception e){Interlocked.Increment(ref Errors);Log("CONSUMER RELEASE ERROR "+e.Message);}
        try{Mouse?.SubmitMouse(new HMMouseState());Log("OFF mouse released "+reason);}catch(Exception e){Interlocked.Increment(ref Errors);Log("MOUSE RELEASE ERROR "+e.Message);}
    }
    static void Stop(string reason)
    {
        Interlocked.Exchange(ref Mode,0);Interlocked.Exchange(ref Armed,0);Interlocked.Increment(ref Epoch);Signal.Set();
        lock(Gate)
        {
            while(Pending.TryDequeue(out _)){}
            ReleaseDevices(reason);
        }
    }
    static bool Enqueue(Input e)
    {
        if(Pending.Count>=256){RequestStop("queue overflow");return false;}
        if(e.Stamp==0)e=e with{Stamp=Stopwatch.GetTimestamp()};
        if(e.Id==0)e=e with{Id=Interlocked.Increment(ref InputSequence)};
        Pending.Enqueue(e);Signal.Set();return true;
    }
    static void SendLoop()
    {
        while(Running)
        {
            Signal.WaitOne(25);
            while(Pending.TryDequeue(out var e))lock(Gate)
            {
                int mode=Volatile.Read(ref Mode);
                if(mode==0 || e.Epoch!=Interlocked.Read(ref Epoch) || (mode&(e.Kind==Kind.Mouse?1:2))==0)continue;
                try
                {
                    if(e.Kind==Kind.Key)
                    {
                        if(!KeysHeld.Set((byte)e.Code,e.Down)){Trace($"KEY unchanged usage={e.Code:X2} down={e.Down}");continue;}
                        var keys=KeysHeld.NonModifiers.Select(u=>(HMKey)u).ToArray();
                        CurrentKeys=keys.Length;PeakKeys=Math.Max(PeakKeys,CurrentKeys);
                        Trace($"KEY SEND begin mods={KeysHeld.Modifiers:X2} held={string.Join(',',keys.Select(u=>((byte)u).ToString("X2")))} age={Clock.ElapsedMilliseconds-e.Tick}");
                        Keyboard!.SubmitKeyboard((HMKeyModifiers)KeysHeld.Modifiers,keys);
                        Interlocked.Increment(ref SentKey);Trace("KEY SEND end");
                    }
                    else if(e.Kind==Kind.Consumer)
                    {
                        bool changed=e.Down?ConsumerHeld.Add((ushort)e.Code):ConsumerHeld.Remove((ushort)e.Code);
                        if(!changed)continue;
                        if(ConsumerHeld.Count>8){RequestStop("consumer rollover limit");continue;}
                        Trace($"CONSUMER SEND begin held={string.Join(',',ConsumerHeld.Select(u=>u.ToString("X")))}");
                        Keyboard!.SubmitConsumer(ConsumerHeld.ToArray());Interlocked.Increment(ref SentConsumer);Trace("CONSUMER SEND end");
                    }
                    else
                    {
                        // Every L/R edge is independent. No chord delay, grouping or middle emulation.
                        if(Volatile.Read(ref Mode)==0 || e.Epoch!=Interlocked.Read(ref Epoch))continue;
                        uint before=MouseHeld.Buttons;
                        var motion=MouseHeld.Apply(e.Code,e.Data);
                        CurrentButtons=(int)MouseHeld.Buttons;
                        Trace($"MOUSE SEND begin id={e.Id} before={before:X2} buttons={MouseHeld.Buttons:X2} wheel={motion.Wheel} pan={motion.Pan} age={Clock.ElapsedMilliseconds-e.Tick} ageUs={(Stopwatch.GetTimestamp()-e.Stamp)*1_000_000.0/Stopwatch.Frequency:F0}");
                        long submitBegin=Stopwatch.GetTimestamp();
                        Mouse!.SubmitMouse(new HMMouseState{Buttons=(HMMouseButtons)MouseHeld.Buttons,Wheel=motion.Wheel,Pan=motion.Pan});
                        Interlocked.Increment(ref SentMouse);
                        Trace($"MOUSE SEND end id={e.Id} buttons={MouseHeld.Buttons:X2} submitUs={(Stopwatch.GetTimestamp()-submitBegin)*1_000_000.0/Stopwatch.Frequency:F0}");
                    }
                }
                catch(Exception ex){Interlocked.Increment(ref Errors);RequestStop("submission error");Log("SEND ERROR "+ex);}
            }
        }
    }
    static IntPtr MouseHook(int code,UIntPtr w,IntPtr l)
    {
        if(code>=0)
        {
            uint msg=(uint)w.ToUInt64();
            if(msg is >=0x201 and <=0x20e && msg is not (0x203 or 0x206 or 0x209 or 0x20d))
            {
                var d=Marshal.PtrToStructure<MouseData>(l);
                if(MouseState.Supported(msg,d.Data))
                {
                    long stamp=Stopwatch.GetTimestamp();
                    bool injected=(d.Flags&1)!=0;int mode=Volatile.Read(ref Mode);long epoch=Interlocked.Read(ref Epoch);
                    long id=injected && (mode&1)!=0?Interlocked.Increment(ref InputSequence):0;
                    if(injected)Interlocked.Increment(ref MouseInjected);else Interlocked.Increment(ref MouseHardware);
                    if(mode!=0)Trace($"MOUSE HOOK seq={Interlocked.Increment(ref Sequence)} id={id} qpc={stamp} sourceTime={d.Time} msg={msg:X} data={d.Data:X8} flags={d.Flags:X} xy={d.Point.X},{d.Point.Y} mode={mode}");
                    if(injected && (mode&1)!=0 && Enqueue(new(Kind.Mouse,msg,d.Data,false,Clock.ElapsedMilliseconds,epoch,stamp,id)))return (IntPtr)1;
                }
            }
        }
        return CallNextHookEx(MouseHookId,code,w,l);
    }
    static IntPtr KeyHook(int code,UIntPtr w,IntPtr l)
    {
        if(code>=0)
        {
            var d=Marshal.PtrToStructure<KeyData>(l);uint msg=(uint)w.ToUInt64();
            bool down=msg is 0x100 or 0x104,up=msg is 0x101 or 0x105;
            if(down || up)
            {
                bool injected=(d.Flags&0x10)!=0;int mode=Volatile.Read(ref Mode);long epoch=Interlocked.Read(ref Epoch);
                byte usage=KeyboardMap.Translate(d.Vk,d.Scan,d.Flags);ushort consumer=KeyboardMap.Consumer(d.Vk);
                if(injected)
                {
                    Interlocked.Increment(ref KeyInjected);
                    if(usage is >=0xe0 and <=0xe7)
                    {int mask=1<<(usage-0xe0);if(down)Interlocked.Or(ref IncomingModifiers,mask);else Interlocked.And(ref IncomingModifiers,~mask);}
                }
                else Interlocked.Increment(ref KeyHardware);
                if(d.Vk==(uint)Volatile.Read(ref StopVk) || (Volatile.Read(ref StopVk)==0x13 && d.Vk==0x03))
                {
                    int mods=Volatile.Read(ref IncomingModifiers);
                    bool chord=(mods&0x11)!=0 && (mods&0x44)!=0;
                    // Also allow a real local keyboard to stop the bridge.
                    if(!injected)chord|=(GetAsyncKeyState(0x11)&0x8000)!=0 && (GetAsyncKeyState(0x12)&0x8000)!=0;
                    if(down && chord && (ReservedStop || mode!=0 || Volatile.Read(ref Armed)!=0))
                    {ReservedStop=true;RequestStop("emergency chord");return (IntPtr)1;}
                    if(up && ReservedStop){ReservedStop=false;return (IntPtr)1;}
                    if(down && !chord)ReservedStop=false;
                }
                if(mode!=0)Trace($"KEY HOOK seq={Interlocked.Increment(ref Sequence)} vk={d.Vk:X} scan={d.Scan:X} flags={d.Flags:X} usage={usage:X2} consumer={consumer:X} down={down} mode={mode}");
                if(injected && (mode&2)!=0)
                {
                    if(consumer!=0)
                    {if(Enqueue(new(Kind.Consumer,consumer,0,down,Clock.ElapsedMilliseconds,epoch)))return (IntPtr)1;}
                    else if(usage!=0)
                    {if(Enqueue(new(Kind.Key,usage,0,down,Clock.ElapsedMilliseconds,epoch)))return (IntPtr)1;}
                    else{Interlocked.Increment(ref Unmapped);Trace($"UNMAPPED KEY vk={d.Vk:X}; original passed through");}
                }
            }
        }
        return CallNextHookEx(KeyHookId,code,w,l);
    }
    delegate IntPtr Proc(int code,UIntPtr w,IntPtr l);
    [StructLayout(LayoutKind.Sequential)]struct Point{public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]struct MouseData{public Point Point;public uint Data,Flags,Time;public UIntPtr Extra;}
    [StructLayout(LayoutKind.Sequential)]struct KeyData{public uint Vk,Scan,Flags,Time;public UIntPtr Extra;}
    [DllImport("user32.dll",SetLastError=true)]static extern IntPtr SetWindowsHookEx(int id,Proc proc,IntPtr module,uint tid);
    [DllImport("user32.dll")]static extern bool UnhookWindowsHookEx(IntPtr h);
    [DllImport("user32.dll")]static extern IntPtr CallNextHookEx(IntPtr h,int c,UIntPtr w,IntPtr l);
    [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr GetModuleHandle(string? name);
}
