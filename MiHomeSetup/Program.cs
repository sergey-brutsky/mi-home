using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib.MqttGateway;
using MiHomeSetup;

if (args.Length == 0 || args is ["--help"])
{
    Console.WriteLine("MiHomeSetup check --host <IP> [--model <model>] [--token <value> | --token-file <path>] [--key <value> | --key-file <path>]\n" +
        "MiHomeSetup setup --host <IP> [--model <model>] [--token <value> | --token-file <path>] [--key <value> | --key-file <path>]\n" +
        "MiHomeSetup disable-lan --host <IP> [--model lumi.gateway.v3] --token <value> | --token-file <path>\n" +
        "MiHomeSetup download-agent --model <model> --output <new-file>\n" +
        "Models: lumi.gateway.v3, lumi.gateway.mgl03, lumi.gateway.mcn001, lumi.gateway.mgl001, lumi.gateway.aqcn02\n" +
        "Gateway 2 check requires a miIO token and native UDP discovery access. Its setup enables LAN access by configuring a developer key and verifies UDP discovery.\n" +
        "MQTT gateway check requires administrative Telnet on port 23 and MQTT on port 1883.\n" +
        "Checks MQTT acknowledgements and discovery files, without publishing device commands.\n" +
        "setup enables Telnet via miIO on known firmware, installs the agent and restarts MQTT.\n" +
        "Telnet activation requires --token or --token-file; newer firmware also requires --key or --key-file.\n" +
        "download-agent downloads and verifies openmiio_agent v1.2.1 locally.\n" +
        "Exit codes: 0 success/ready, 1 failure/not ready, 2 invalid arguments, 130 cancelled.");
    return 0;
}

SetupOptions options;
try
{
    options = SetupOptions.Parse(args);
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}
var target = options.Target;

using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(15));
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
try
{
    var info = options.Model is null
        ? await GatewayModelDetector.DetectAsync(options, cancellation.Token, Console.Out)
        : new GatewayInfo(options.Model, null);
    var model = info.Model;
    options = options with { Model = model };
    if (model == Gateway2Checker.Model)
    {
        return await Gateway2Checker.RunAsync(options, info, cancellation.Token, Console.Out) ? 0 : 1;
    }
    if (options.Command == "disable-lan")
        throw new InvalidOperationException("disable-lan supports only Gateway 2 (lumi.gateway.v3). The detected gateway is a different model.");
    var profile = GatewayProfile.Resolve(model);
    if (options.Command == "check") await options.ReadKeyAsync(cancellation.Token);
    if (options.Command == "download-agent")
    {
        var binary = await GatewayInstaller.DownloadAsync(profile, cancellation.Token, Console.Out);
        await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write);
        await output.WriteAsync(binary, cancellation.Token);
        Console.WriteLine("Verified agent saved to " + Path.GetFullPath(target));
        return 0;
    }
    if (options.Command == "setup")
    {
        await TelnetActivator.EnsureAsync(options, cancellation.Token, Console.Out);
        using var shell = new GatewayShell();
        await shell.ConnectAsync(target, profile, cancellation.Token);
        await GatewayInstaller.VerifyAsync(shell, profile, model, cancellation.Token, Console.Out);
        await shell.RunAsync("mkdir /tmp/mihome-setup.lock", cancellation.Token);
        try
        {
            var readiness = await CheckAsync(target, profile, cancellation.Token);
            if (!readiness.Discovery)
                throw new InvalidOperationException("Discovery files cannot be read. Preparing MQTT will not repair discovery; resolve the reported discovery error first.");
            await GatewayInstaller.InstallAsync(shell, profile, async token =>
                (await CheckAsync(target, profile, token)).Ready, cancellation.Token, output: Console.Out, error: Console.Error);
            return 0;
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await shell.RunAsync("rmdir /tmp/mihome-setup.lock", cleanup.Token); }
            catch (Exception) { Console.Error.WriteLine("Could not release /tmp/mihome-setup.lock. Remove the directory after confirming no setup process is running."); }
        }
    }
    var result = await CheckAsync(target, profile, cancellation.Token);
    if (!result.Ready)
    {
        if (info.Firmware is null && (options.Token is not null || options.TokenFile is not null))
        {
            try { info = await GatewayModelDetector.ReadMiioInfoAsync(options, cancellation.Token); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Console.WriteLine("Could not read firmware information through miIO.");
            }
        }
        GatewayCheckAdvice.Write(options, result, info, Console.Out);
    }
    return result.Ready ? 0 : 1;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Operation cancelled or timed out.");
    return cancellation.IsCancellationRequested ? 130 : 1;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

static async Task<GatewayReadiness> CheckAsync(string host, GatewayProfile profile, CancellationToken token)
{
    var mqttReady = true;
    var discoveryReady = true;
    Exception? discoveryError = null;
    try
    {
        token.ThrowIfCancellationRequested();
        using var mqtt = new MqttTransport(host, 1883, profile.BlePath.Length == 0 ? ["zigbee/send"] : ["zigbee/send", "central/report"], "zigbee/recv", NullLoggerFactory.Instance);
        Console.WriteLine("PASS MQTT connection and subscriptions");
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        Console.WriteLine("FAIL MQTT: " + GatewayCheckAdvice.DescribeError(exception, 1883));
        mqttReady = false;
    }
    var discovery = new CommonDevicesDiscoverer(host, 23, profile.Login, profile.Prompt,
        "/data/zigbee/device.info", profile.BlePath);
    try
    {
        var zigbee = await discovery.DiscoverZigBeeDevicesAsync(token);
        Console.WriteLine("PASS Zigbee discovery (" + zigbee.Count + " devices)");
        if (profile.BlePath.Length > 0)
        {
            var ble = await discovery.DiscoverBleDevicesAsync(token);
            var mesh = await discovery.DiscoverMeshDevicesAsync(token);
            Console.WriteLine($"PASS BLE/Mesh discovery ({ble.Count}/{mesh.Count} devices)");
        }
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        Console.WriteLine("FAIL Discovery: " + GatewayCheckAdvice.DescribeError(exception, 23));
        discoveryError = exception;
        discoveryReady = false;
    }
    var readiness = new GatewayReadiness(mqttReady, discoveryReady, discoveryError);
    Console.WriteLine(readiness.Ready ? "READY (MQTT and discovery; miIO commands require a separate valid token)" : "NOT READY");
    return readiness;
}
