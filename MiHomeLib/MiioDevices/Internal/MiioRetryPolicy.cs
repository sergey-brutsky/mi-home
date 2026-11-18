namespace MiHomeLib.MiioDevices.Internal;

internal enum MiioRetryPolicy { SafeToRepeat, DoNotRepeat }

internal static class MiioRetry
{
    // Unknown commands and actions are sent once: a lost response does not prove they were not executed.
    // MI-spec writes select their policy explicitly: set_properties may represent an action too.
    public static MiioRetryPolicy ForMethod(string method) => method switch
    {
        "miIO.info" or "get_prop" or "get_properties" or "get_status" or "get_channels" or
        "get_lumi_dpf_aes_key" or "get_arming" or "get_arm_wait_time" or "get_device_prop" or
        "get_alarming_volume" or "get_arming_time" or
        "set_power" or "set_mode" or "set_led_b" or "set_buzzer" or "set_child_lock" or
        "set_lumi_dpf_aes_key" or "set_rgb" or "set_night_light_rgb" or "set_arming" or
        "set_arm_wait_time" or "set_device_prop" or "set_alarming_volume" => MiioRetryPolicy.SafeToRepeat,
        _ => MiioRetryPolicy.DoNotRepeat
    };
}
