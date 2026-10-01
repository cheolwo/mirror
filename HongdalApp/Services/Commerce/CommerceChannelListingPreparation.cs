using System.Text.Json.Nodes;

namespace HongdalApp.Services.Commerce;

public sealed record CommerceChannelListingPreparation(
    CommerceChannelDescriptor Channel,
    JsonNode? PayloadDraft,
    string SyncStatus,
    string Message);
