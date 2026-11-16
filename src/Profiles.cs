using HIDMaestro;

internal static class Profiles
{
    public static HidDescriptorBuilder KeyboardDescriptor() => new HidDescriptorBuilder()
        .KeyboardNkro(reportId:1).ConsumerControl(slots:8,reportId:2);
    public static HMProfile Keyboard(HMProfile baseline) => new HMProfileBuilder()
        .FromProfile(baseline).Id("moonlight-bridge-nkro-v5")
        .Name("Moonlight Bridge NKRO + Media").ClearDeviceModel()
        .FromDescriptorBuilder(KeyboardDescriptor()).Type("keyboard").Build();
    public static void SelfTest()
    {
        var descriptor=KeyboardDescriptor();
        if(descriptor.InputReportByteSize!=29 || !descriptor.DescriptorContainsReportId())
            throw new Exception("NKRO report sizing failed");
        var p=new HMProfileBuilder().Id("bridge-profile-selftest").Name("Bridge test")
            .Vid(0x045e).Pid(0x0750).Type("keyboard").Connection("usb")
            .FromDescriptorBuilder(descriptor).Build();
        if(p.Type!="keyboard")throw new Exception("Keyboard profile type failed");
    }
}
