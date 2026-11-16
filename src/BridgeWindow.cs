using System.Diagnostics;
using System.Drawing;

internal readonly record struct BridgeSnapshot(bool Active,bool Armed,bool Cancelled,long Errors,long MouseReports,long KeyReports);

internal sealed class BridgeWindow : Form
{
    readonly Func<int,bool,int,string?> activate;
    readonly Action deactivate;
    readonly Func<BridgeSnapshot> snapshot;
    readonly Action<bool,int> setArmed;
    readonly string logDirectory;
    readonly Button toggle=new(){Width=520,Height=64,Font=new Font("Segoe UI",16,FontStyle.Bold)};
    readonly Button language=new(){AutoSize=true,Text="English"};
    readonly Button logs=new(){AutoSize=true};
    readonly Label status=new(){AutoSize=true,Font=new Font("Segoe UI",14,FontStyle.Bold)};
    readonly Label subtitle=new(){AutoSize=true,MaximumSize=new Size(520,0)};
    readonly Label emergency=new(){AutoSize=true,MaximumSize=new Size(520,0)};
    readonly Label summary=new(){AutoSize=true,MaximumSize=new Size(520,0)};
    readonly Label durationLabel=new(){AutoSize=true};
    readonly CheckBox advanced=new(){AutoSize=true};
    readonly CheckBox diagnostic=new(){AutoSize=true,Checked=false};
    readonly ComboBox duration=new(){Width=240,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly ComboBox stopKey=new(){Width=300,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly FlowLayoutPanel extra=new(){AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Visible=false};
    readonly TestField test=new(){Width=520,Height=110,Multiline=true,AcceptsReturn=true,AcceptsTab=true,BackColor=Color.LightSkyBlue};
    readonly System.Windows.Forms.Timer clock=new(){Interval=100};
    bool english,pending,closing;
    long deadline;
    string? notice;

    internal BridgeWindow(Func<int,bool,int,string?> activate,Action deactivate,Func<BridgeSnapshot> snapshot,
        Action<bool,int> setArmed,string logDirectory)
    {
        this.activate=activate;this.deactivate=deactivate;this.snapshot=snapshot;this.setArmed=setArmed;this.logDirectory=logDirectory;
        Text="aion2-remote-play v6";Font=new Font("Segoe UI",10);ClientSize=new Size(590,340);
        MinimumSize=new Size(580,370);StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.Dpi;
        var layout=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(22),FlowDirection=FlowDirection.TopDown,WrapContents=false};
        layout.Controls.Add(language);layout.Controls.Add(status);layout.Controls.Add(subtitle);layout.Controls.Add(toggle);
        layout.Controls.Add(emergency);layout.Controls.Add(advanced);layout.Controls.Add(extra);
        extra.Controls.Add(durationLabel);extra.Controls.Add(duration);extra.Controls.Add(stopKey);extra.Controls.Add(diagnostic);extra.Controls.Add(summary);extra.Controls.Add(test);extra.Controls.Add(logs);
        Controls.Add(layout);
        duration.Items.AddRange(new object[]{"","",""});duration.SelectedIndex=0;
        stopKey.Items.AddRange(new object[]{"Ctrl+Alt+Pause","Ctrl+Alt+ScrollLock"});stopKey.SelectedIndex=0;
        toggle.Click+=(_,_)=>Toggle();
        duration.SelectedIndexChanged+=(_,_)=>{if(duration.SelectedIndex!=1)diagnostic.Checked=false;RefreshState();};
        stopKey.SelectedIndexChanged+=(_,_)=>RefreshState();
        language.Click+=(_,_)=>{english=!english;Translate();};
        advanced.CheckedChanged+=(_,_)=>{extra.Visible=advanced.Checked;ClientSize=new Size(ClientSize.Width,advanced.Checked?680:340);};
        logs.Click+=(_,_)=>
        {
            try{Directory.CreateDirectory(logDirectory);Process.Start(new ProcessStartInfo(logDirectory){UseShellExecute=true});}
            catch{MessageBox.Show(logDirectory,"Logs / Журналы");}
        };
        clock.Tick+=(_,_)=>Tick();
        FormClosing+=(_,_)=>{closing=true;pending=false;clock.Stop();setArmed(false,SelectedStopKey);deactivate();};
        FormClosed+=(_,_)=>clock.Dispose();
        Translate();clock.Start();
    }
    int SelectedStopKey=>stopKey.SelectedIndex==1?0x91:0x13;
    string T(string ru,string en)=>english?en:ru;
    void Translate()
    {
        language.Text=english?"Русский":"English";
        advanced.Text=T("Дополнительно","Advanced");
        durationLabel.Text=T("Длительность работы","Session duration");
        int selected=duration.SelectedIndex;
        duration.Items[0]=T("До выключения","Until switched off");duration.Items[1]=T("Тест: 90 секунд","Test: 90 seconds");duration.Items[2]=T("10 минут","10 minutes");duration.SelectedIndex=selected;
        diagnostic.Text=T("Записывать коды клавиш (только тест 90 с)","Record key codes (90-second test only)");
        logs.Text=T("Открыть папку журналов","Open log folder");
        test.PlaceholderText=T("Тестовое поле: печатайте здесь после включения","Test field: type here after enabling");
        RefreshState();
    }
    internal void Toggle()
    {
        var state=snapshot();
        if(pending || state.Active || state.Armed)
        {
            pending=false;setArmed(false,SelectedStopKey);deactivate();notice=null;RefreshState();return;
        }
        notice=null;pending=true;deadline=Stopwatch.GetTimestamp()+3*Stopwatch.Frequency;
        setArmed(true,SelectedStopKey);RefreshState();
    }
    void Tick()
    {
        if(closing)return;
        var state=snapshot();
        if(pending && state.Cancelled){pending=false;setArmed(false,SelectedStopKey);}
        if(pending && Stopwatch.GetTimestamp()>=deadline)CompleteActivation();
        RefreshState();
    }
    internal void CompleteActivation()
    {
        if(!pending)return;
        if(snapshot().Cancelled){pending=false;setArmed(false,SelectedStopKey);RefreshState();return;}
        pending=false;setArmed(false,SelectedStopKey);
        notice=activate(duration.SelectedIndex switch{1=>90,2=>600,_=>0},diagnostic.Checked,SelectedStopKey);
        RefreshState();
    }
    internal void RefreshState()
    {
        var s=snapshot();bool idle=!pending && !s.Active && !s.Armed;
        duration.Enabled=stopKey.Enabled=idle;diagnostic.Enabled=idle && duration.SelectedIndex==1;
        emergency.Text=T("Быстрое отключение: ","Emergency OFF: ")+stopKey.Text;
        toggle.Text=pending?T("Отменить включение","Cancel activation"):s.Active?T("Выключить","Turn off"):T("Включить","Turn on");
        toggle.BackColor=s.Active?Color.LightCoral:Color.LightGreen;
        status.Text=pending?T("Включение через ","Starting in ")+Math.Max(1,(int)Math.Ceiling((deadline-Stopwatch.GetTimestamp())/(double)Stopwatch.Frequency))+T(" с"," s"):
            s.Active?T("Работает: мышь и клавиатура","Active: mouse and keyboard"):
            notice!=null?T("Не включено","Not activated"):
            s.Errors>0?T("Отключено после ошибки — см. журнал","Off — errors recorded; see log"):T("Выключено","Off");
        subtitle.Text=notice??T("Одна кнопка включает мышь и клавиатуру. ЛКМ и ПКМ независимы; СКМ — только отдельная кнопка.","One button enables mouse and keyboard. Left and right remain independent; middle is a separate button.");
        summary.Text=T("Отчёты: мышь ","Reports: mouse ")+s.MouseReports+T(", клавиатура ",", keyboard ")+s.KeyReports+T("; ошибки ","; errors ")+s.Errors;
    }
    internal static void SelfTest()
    {
        bool active=false,armed=false,cancel=false;int starts=0,stops=0;long errors=0;bool fail=false;
        using var f=new BridgeWindow((seconds,detail,key)=>{starts++;if(seconds!=0 || detail || key!=0x13)throw new Exception("Unsafe default settings");if(fail)return "Held keys";active=true;return null;},
            ()=>{active=false;armed=false;stops++;},()=>new(active,armed,cancel,errors,0,0),
            (value,key)=>{armed=value;if(value)cancel=false;},Path.GetTempPath());
        f.CreateControl();
        f.Toggle();if(!armed || active || starts!=0)throw new Exception("Activation must arm first");
        f.Toggle();f.CompleteActivation();if(active || armed || starts!=0)throw new Exception("Cancelled arm activated");
        f.Toggle();f.CompleteActivation();if(!active || armed || starts!=1)throw new Exception("One-button activation failed");
        f.Toggle();if(active || stops!=2)throw new Exception("One-button stop failed");
        f.Toggle();cancel=true;armed=false;f.CompleteActivation();if(active || starts!=1)throw new Exception("Emergency during arm failed");
        fail=true;f.Toggle();f.CompleteActivation();if(active || f.notice==null)throw new Exception("Held-key activation failure hidden");
        fail=false;f.Toggle();f.CompleteActivation();active=false;errors=1;f.RefreshState();if(f.status.Text.Contains("Работает"))throw new Exception("Fault state hidden");
        f.english=true;f.Translate();if(f.toggle.Text!="Turn on")throw new Exception("UI language failed");
        f.Close();if(active)throw new Exception("Close did not stop");
    }
}

internal sealed class TestField:TextBox
{
    public event Action<uint,uint,string>? Observed;
    protected override bool IsInputKey(Keys keyData)=>true;
    protected override void WndProc(ref Message m)
    {
        // Keep the diagnostic field from opening a context menu after every RMB release.
        if(m.Msg==0x007b){m.Result=IntPtr.Zero;return;}
        uint msg=(uint)m.Msg,v=unchecked((uint)m.WParam.ToInt64());
        if(msg is 0x100 or 0x104 or 0x101 or 0x105)
            Observed?.Invoke(msg,v,$"KEY {(msg is 0x100 or 0x104?"DOWN":"UP")} vk={v:X}");
        else if(MouseState.Supported(msg,v))
        {
            // Window message wParam packs button / wheel in the high word, as MSLLHOOKSTRUCT.mouseData does.
            string button=msg switch{0x201 or 0x202=>"LEFT",0x204 or 0x205=>"RIGHT",0x207 or 0x208=>"MIDDLE",0x20b or 0x20c=>(v>>16)==1?"BACK":"FORWARD",_=>"WHEEL"};
            Observed?.Invoke(msg,v,$"MOUSE {button} msg={msg:X} delta={unchecked((short)(v>>16))}");
        }
        base.WndProc(ref m);
    }
}
