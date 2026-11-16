internal sealed class MouseState
{
    public uint Buttons {get;private set;}
    int verticalRemainder,horizontalRemainder;
    public static bool Supported(uint msg,uint data) => msg is 0x201 or 0x202 or 0x204 or 0x205 or 0x207 or 0x208 or 0x20a or 0x20e || ((msg is 0x20b or 0x20c) && ((data>>16) is 1 or 2));
    public (int Wheel,int Pan) Apply(uint msg,uint data)
    {
        uint mask=msg switch {0x201 or 0x202=>1,0x204 or 0x205=>2,0x207 or 0x208=>4,0x20b or 0x20c=>(data>>16)==1?8u:16u,_=>0};
        if(mask!=0) {bool down=msg is 0x201 or 0x204 or 0x207 or 0x20b;Buttons=down?Buttons|mask:Buttons&~mask;}
        int wheel=0,pan=0,delta=unchecked((short)(data>>16));
        if(msg==0x20a) {verticalRemainder+=delta;wheel=verticalRemainder/120;verticalRemainder%=120;}
        if(msg==0x20e) {horizontalRemainder+=delta;pan=horizontalRemainder/120;horizontalRemainder%=120;}
        return (wheel,pan);
    }
    public void Clear() {Buttons=0;verticalRemainder=horizontalRemainder=0;}
    public static void SelfTest()
    {
        var s=new MouseState();
        s.Apply(0x201,0);s.Apply(0x204,0);s.Apply(0x207,0);s.Apply(0x20b,1u<<16);s.Apply(0x20b,2u<<16);
        if(s.Buttons!=31)throw new Exception("Five-button chord failed");
        s.Apply(0x20c,1u<<16);s.Apply(0x208,0);
        if(s.Buttons!=19)throw new Exception("Independent mouse release failed");
        if(s.Apply(0x20a,60u<<16).Wheel!=0 || s.Apply(0x20a,60u<<16).Wheel!=1)throw new Exception("Fractional wheel accumulation failed");
        if(s.Apply(0x20a,unchecked((uint)(ushort)(short)-240)<<16).Wheel!=-2)throw new Exception("Negative wheel failed");
        if(s.Apply(0x20e,120u<<16).Pan!=1 || s.Buttons!=19)throw new Exception("Horizontal wheel changed button state");
        if(Supported(0x20b,3u<<16) || !Supported(0x20c,2u<<16))throw new Exception("Unknown X button handling failed");
        // Every L/R ordering, including simultaneous-style consecutive edges, must keep Middle clear.
        foreach(var downOrder in new[]{new uint[]{0x201,0x204},new uint[]{0x204,0x201}})
        foreach(var upOrder in new[]{new uint[]{0x202,0x205},new uint[]{0x205,0x202}})
        {
            s.Clear();s.Apply(downOrder[0],0);s.Apply(downOrder[1],0);
            if(s.Buttons!=3)throw new Exception("L/R chord became middle");
            s.Apply(upOrder[0],0);if((s.Buttons&4)!=0 || s.Buttons==0)throw new Exception("L/R independent release failed");
            s.Apply(upOrder[1],0);if(s.Buttons!=0)throw new Exception("L/R release stuck");
            s.Apply(0x207,0);s.Apply(0x201,0);s.Apply(0x204,0);
            if(s.Buttons!=7)throw new Exception("Physical middle chord lost");
            s.Apply(0x208,0);if(s.Buttons!=3)throw new Exception("Middle release affected L/R");
        }
        s.Clear();if(s.Buttons!=0 || s.Apply(0x20a,60u<<16).Wheel!=0)throw new Exception("Mouse neutral/reset failed");
    }
}
