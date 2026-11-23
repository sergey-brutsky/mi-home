using MiHomeLib.Helpers;
using MiHomeLib.MiioDevices.Internal;
using MiHomeLib.Devices;
using MiHomeLib.Protocols;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib.MiioDevices;


namespace MiHomeLib.XiaomiGateway2;

public partial class XiaomiGateway2
{
    /// <summary>
    /// Get list or radio channels from gateway in format {id: &lt;int&gt;, url: &lt;url&gt;}
    /// </summary>
    /// <returns>List of RadioChannel</returns>
    public List<RadioChannel> GetRadioChannels() => RunSync(GetRadioChannelsAsync);

    /// <summary>
    /// Get list or radio channels from gateway in format {id: &lt;int&gt;, url: &lt;url&gt;}
    /// </summary>
    /// <returns>List of RadioChannel</returns>
    public async Task<List<RadioChannel>> GetRadioChannelsAsync()
    {
        var response = await RequestObjectAsync("get_channels", new RadioStart(0)).ConfigureAwait(false);
        using var document = MiioResponseReader.Parse(response, "Unable to get radio channels");
        var channels = document.RootElement.GetProperty("result").GetProperty("chs");
        return channels.Deserialize(MiHomeJsonContext.Default.ListRadioChannel);
    }

    /// <summary>
    /// Add custom radio channel to the gateway
    /// </summary>
    public void AddRadioChannel(int channelId, string channelUrl) => RunSync(() => AddRadioChannelAsync(channelId, channelUrl));

    /// <summary>
    /// Add custom radio channel to the gateway
    /// </summary>
    public async Task AddRadioChannelAsync(int channelId, string channelUrl)
    {
        if (channelId < 1024) throw new ArgumentOutOfRangeException(nameof(channelId), channelId, "Radio channel id must be >= 1024");

        if ((await GetRadioChannelsAsync().ConfigureAwait(false)).Any(x => x.Id == channelId))
            throw new ArgumentException($"Radio channel with id {channelId} already exists, choose another id", nameof(channelId));

        var result = await RequestObjectAsync("add_channels", new RadioChannels(new List<RadioChannel> { new() { Id = channelId, Url = channelUrl, Type = 0 } })).ConfigureAwait(false);

        CheckMessage(result, "Unable to add radio channel");
    }

    /// <summary>
    /// Remove custom radio channel from gateway stations list
    /// </summary>
    public void RemoveRadioChannel(int channelId) => RunSync(() => RemoveRadioChannelAsync(channelId));

    /// <summary>
    /// Remove custom radio channel from gateway stations list
    /// </summary>
    public async Task RemoveRadioChannelAsync(int channelId)
    {
        var radioChannels = await GetRadioChannelsAsync().ConfigureAwait(false);

        if (!radioChannels.Any(x => x.Id == channelId))
            throw new ArgumentException($"Radio channel with id {channelId} doesn't exist", nameof(channelId));

        radioChannels.RemoveAll(x => x.Id != channelId);

        var result = await RequestObjectAsync("remove_channels", new RadioChannels(radioChannels)).ConfigureAwait(false);

        CheckMessage(result, $"Unable to remove radio channel with id {channelId}");
    }

    /// <summary>
    /// Clear all custom radio channels from the gateway
    /// </summary>
    public void RemoveAllRadioChannels() => RunSync(RemoveAllRadioChannelsAsync);

    /// <summary>
    /// Clear all custom radio channels from the gateway
    /// </summary>
    public async Task RemoveAllRadioChannelsAsync()
    {
        var radioChannels = await GetRadioChannelsAsync().ConfigureAwait(false);
        var result = await RequestObjectAsync("remove_channels", new RadioChannels(radioChannels)).ConfigureAwait(false);
        CheckMessage(result, "Unable to remove all radio channels");
    }

    /// <summary>
    /// Start playing custom channel
    /// </summary>
    public void PlayRadio(int channelId, int volume) => RunSync(() => PlayRadioAsync(channelId, volume));

    /// <summary>
    /// Start playing custom channel
    /// </summary>
    public async Task PlayRadioAsync(int channelId, int volume)
    {
        if (volume < 0 || volume > 100)
            throw new ArgumentOutOfRangeException(nameof(volume), volume, "Volume must be within range 0-100");

        if (!(await GetRadioChannelsAsync().ConfigureAwait(false)).Any(x => x.Id == channelId))
            throw new ArgumentException($"Radio channel with id {channelId} doesn't exist", nameof(channelId));

        var result = await RequestArrayAsync("play_specify_fm", channelId, volume).ConfigureAwait(false);
        CheckMessage(result, $"Unable to play channelId: {channelId} with volume {volume}");
    }

    /// <summary>
    /// Stop playing radio
    /// </summary>
    public void StopRadio() => RunSync(StopRadioAsync);

    /// <summary>
    /// Stop playing radio
    /// </summary>
    public async Task StopRadioAsync()
    {
        var result = await RequestArrayAsync("play_fm", "off").ConfigureAwait(false);
        CheckMessage(result, $"Unable to stop playing radio");
    }

    public class RadioChannel
    {
        public int Id { get; set; }
        public string Url { get; set; }
        public int Type { get; set; }

        public override string ToString()
        {
            return $"Radio channel -> Id: {Id}, Url: {Url}";
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;

            RadioChannel other = (RadioChannel)obj;

            return Id == other.Id && Url == other.Url;
        }

        public override int GetHashCode()
        {
            return new { Id, Url }.GetHashCode();
        }
    }

}
