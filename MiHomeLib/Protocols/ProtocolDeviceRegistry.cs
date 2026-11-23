using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using MiHomeLib.Devices;

namespace MiHomeLib.Protocols;

internal static class ProtocolDeviceRegistry
{
    private static readonly IReadOnlyList<Registration> Registrations =
        ValidateRegistrations(ProtocolDeviceCatalog.Entries);

    private static readonly Dictionary<(DeviceProtocol, string), Registration> Models = Registrations
        .ToDictionary(x => (x.Metadata.Protocol, x.Metadata.Model), x => x);
    private static readonly Dictionary<(DeviceProtocol, int), Registration> Pdids = Registrations
        .Where(x => x.Metadata.Protocol is DeviceProtocol.LumiBle or DeviceProtocol.LumiMesh)
        .ToDictionary(x => (x.Metadata.Protocol, x.Metadata.Pdid), x => x);

    internal static IReadOnlyList<Registration> ValidateRegistrations(IEnumerable<ProtocolDeviceEntry> entries)
    {
        var registrations = new List<Registration>();
        var models = new HashSet<(DeviceProtocol, string)>();
        var pdids = new HashSet<(DeviceProtocol, int)>();
        foreach (var entry in entries)
        {
            var type = entry.DeviceType;
            foreach (var metadata in type.GetCustomAttributes(typeof(ProtocolDeviceModelAttribute), false).Cast<ProtocolDeviceModelAttribute>())
            {
                var bleOrMesh = metadata.Protocol is DeviceProtocol.LumiBle or DeviceProtocol.LumiMesh;
                if (type.IsAbstract || !typeof(ProtocolMappedDeviceBase).IsAssignableFrom(type) ||
                    (bleOrMesh ? entry.CreateByPdid is null : entry.Create is null))
                    throw new InvalidOperationException($"Device '{type.FullName}' has an invalid base type or constructor for {metadata.Protocol}.");
                if (string.IsNullOrEmpty(metadata.Model) || !models.Add((metadata.Protocol, metadata.Model)))
                    throw new InvalidOperationException($"Duplicate or empty model '{metadata.Model}' for {metadata.Protocol}.");
                if (bleOrMesh && (metadata.Pdid <= 0 || !pdids.Add((metadata.Protocol, metadata.Pdid))))
                    throw new InvalidOperationException($"Duplicate or invalid PDID '{metadata.Pdid}' for {metadata.Protocol}.");
                registrations.Add(new Registration(metadata, bleOrMesh ? null : entry.Create, bleOrMesh ? entry.CreateByPdid : null));
            }
        }
        return registrations;
    }

    public static IEnumerable<string> Find(DeviceProtocol protocol) =>
        Registrations.Where(x => x.Metadata.Protocol == protocol)
            .Select(x => x.Metadata.Model);

    internal static bool IsSupported(DeviceProtocol protocol, string model) =>
        model is not null && Models.ContainsKey((protocol, model));

    public static bool TryCreateBle(
        int pdid,
        string id,
        ILoggerFactory loggerFactory,
        out GatewaySubDeviceBase device)
        => TryCreateByPdid(DeviceProtocol.LumiBle, pdid, id, loggerFactory, out device);

    public static bool TryCreateMesh(int pdid, string id, ILoggerFactory loggerFactory, out GatewaySubDeviceBase device)
        => TryCreateByPdid(DeviceProtocol.LumiMesh, pdid, id, loggerFactory, out device);

    private static bool TryCreateByPdid(DeviceProtocol protocol, int pdid, string id,
        ILoggerFactory loggerFactory, out GatewaySubDeviceBase device)
    {
        if (!Pdids.TryGetValue((protocol, pdid), out var registration))
        {
            device = null;
            return false;
        }

        device = registration.Create(id, 0, loggerFactory);
        return true;
    }

    public static bool TryCreate(
        DeviceProtocol protocol,
        string model,
        string id,
        int shortId,
        ILoggerFactory loggerFactory,
        out GatewaySubDeviceBase device)
    {
        if (model is null || !Models.TryGetValue((protocol, model), out var registration))
        {
            device = null;
            return false;
        }

        device = registration.Create(id, shortId, loggerFactory);
        return true;
    }

    internal sealed class Registration(ProtocolDeviceModelAttribute metadata,
        Func<string, int, ILoggerFactory, GatewaySubDeviceBase> createByModel,
        Func<string, ILoggerFactory, GatewaySubDeviceBase> createByPdid)
    {
        public ProtocolDeviceModelAttribute Metadata { get; } = metadata;
        public Func<string, int, ILoggerFactory, GatewaySubDeviceBase> CreateByModel { get; } = createByModel;
        public Func<string, ILoggerFactory, GatewaySubDeviceBase> CreateByPdid { get; } = createByPdid;

        internal GatewaySubDeviceBase Create(string id, int shortId, ILoggerFactory loggerFactory)
        {
            var device = CreateByModel is not null
                ? CreateByModel(id, shortId, loggerFactory)
                : CreateByPdid(id, loggerFactory);
            try
            {
                ((ProtocolMappedDeviceBase)device).InitializeProtocol(Metadata.Protocol, Metadata.Model);
                return device;
            }
            catch (Exception initializationFailure)
            {
                try { device.Dispose(); }
                catch (Exception cleanupFailure)
                {
                    initializationFailure.Data["DeviceCleanupException"] = cleanupFailure;
                }
                throw;
            }
        }
    }

}
