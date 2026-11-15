using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ssalddel.Sagajeong.Core;

if (args.Length != 2) throw new ArgumentException("Pass delivery-sample.json and road-graph.json.");
var sampleJson = File.ReadAllText(args[0]);
var graphJson = File.ReadAllText(args[1]);
var assertions = 0;
void Check(bool value, string message)
{
    assertions++;
    if (!value) throw new InvalidOperationException(message);
}
SagajeongSimulationCore Create() => new(sampleJson, graphJson);
JObject Send(SagajeongSimulationCore core, object request) => JObject.Parse(core.Execute(JsonConvert.SerializeObject(request)));
JObject Start(SagajeongSimulationCore core, bool manual, string id) => Send(core, new { action = "start", manualPickup = manual, sessionId = id });
JObject Snapshot(SagajeongSimulationCore core) => Send(core, new { action = "snapshot" });
JArray Orders(JObject value) => (JArray)value["state"]!["orders"]!;
JObject Order(JObject value, string id) => (JObject)Orders(value).Single(order => (string?)order["id"] == id);

var automatic = Create();
var automaticStart = Start(automatic, false, "automatic-parity");
Check((bool)automaticStart["ok"]!, "Automatic legacy start failed.");
Check(Orders(automaticStart).Count == 2, "Initial campaign has two orders.");
foreach (var id in Orders(automaticStart).Select(value => (string)value["id"]!).ToArray())
{
    var candidates = Send(automatic, new { action = "candidates", orderId = id });
    Check((bool)candidates["ok"]!, "Candidate preview failed.");
    var driver = (string)candidates["recommendedDriverId"]!;
    var assignment = new { action = "assign", orderId = id, driverId = driver, revision = (long)candidates["revision"]!, commandId = "auto-"+id, elapsedMs = 0 };
    var assigned = Send(automatic, assignment);
    Check((bool)assigned["ok"]!, "Eligible assignment failed.");
    var repeated = Send(automatic, assignment);
    Check((bool)repeated["ok"]! && JToken.DeepEquals(assigned["state"], repeated["state"]), "Duplicate assignment changed state.");
}
Send(automatic, new { action = "tick", elapsedMs = 120_000 });
var automaticEnd = Send(automatic, new { action = "tick", elapsedMs = 360_000 });
Check((bool)automaticEnd["state"]!["settled"]!, "Legacy automatic session no longer settles.");
Check((int)automaticEnd["state"]!["settlement"]!["revenue"]! == 9600, "Legacy revenue changed.");
Check((int)automaticEnd["state"]!["settlement"]!["driverPay"]! == 7200, "Legacy driver pay changed.");
Check((int)automaticEnd["state"]!["settlement"]!["balance"]! == 3600, "Legacy balance/bonus changed.");
var afterAutomaticEnd = Send(automatic, new { action = "tick", elapsedMs = 600_000 });
Check(JToken.DeepEquals(automaticEnd["state"],afterAutomaticEnd["state"]), "Settled round did not preserve the legacy frozen clock.");
var automaticSave=Send(automatic,new { action="save" });
var automaticRestored=Create();
var automaticReplay=Send(automaticRestored,new { action="restore",saveJson=(string)automaticSave["saveJson"]! });
Check((bool)automaticReplay["ok"]! && JToken.DeepEquals(automaticEnd["state"],automaticReplay["state"]), "Completed-round replay replaced the first settling tick.");

var manual = Create();
var initial = Start(manual, true, "manual-pickup-check");
Check((bool)initial["state"]!["manualPickup"]!, "Manual pickup mode was not applied.");
var assignments = new Dictionary<string,string>();
foreach (var id in Orders(initial).Select(value => (string)value["id"]!).ToArray())
{
    var preview = Send(manual, new { action = "candidates", orderId = id });
    var driver = (string)preview["recommendedDriverId"]!;
    assignments[id] = driver;
    var assigned = Send(manual, new { action = "assign", orderId = id, driverId = driver,
        revision = (long)preview["revision"]!, commandId = "manual-"+id, elapsedMs = 0 });
    Check((bool)assigned["ok"]!, "Manual eligible assignment failed.");
    var premature = Send(manual, new { action = "pickup-confirm", orderId = id, driverId = driver,
        revision = (long)Order(assigned,id)["revision"]!, commandId = "early-"+id, elapsedMs = 0 });
    Check(!(bool)premature["ok"]!, "Pickup accepted before arrival.");
}
var waiting = Send(manual, new { action = "tick", elapsedMs = 3_600_000 });
Check(Orders(waiting).All(order => (string?)order["stage"] == "AtPickup"), "Manual mode automatically left store.");
Check(Orders(waiting).All(order => (bool)order["canConfirmPickup"]!), "Ready pickup was not offered.");
Check(!(bool)waiting["state"]!["settled"]!, "Manual mode settled before confirmation.");

var saved = Send(manual, new { action = "save" });
var restored = Create();
var replay = Send(restored, new { action = "restore", saveJson = (string)saved["saveJson"]! });
Check((bool)replay["ok"]!, "Save restore failed.");
Check(JToken.DeepEquals(waiting["state"], replay["state"]), "Restored snapshot differs from saved state.");
foreach (var pair in assignments)
{
    var current = Snapshot(manual);
    var order = Order(current,pair.Key);
    var revision = (long)order["revision"]!;
    var wrongDriver = Send(manual, new { action = "pickup-confirm", orderId = pair.Key, driverId = "not-assigned",
        revision, commandId = "wrong-"+pair.Key, elapsedMs = 3_600_000 });
    Check(!(bool)wrongDriver["ok"]!, "Wrong driver pickup was accepted.");
    var stale = Send(manual, new { action = "pickup-confirm", orderId = pair.Key, driverId = pair.Value,
        revision = revision-1, commandId = "stale-"+pair.Key, elapsedMs = 3_600_000 });
    Check(!(bool)stale["ok"]!, "Stale pickup revision was accepted.");
    var request = new { action = "pickup-confirm", orderId = pair.Key, driverId = pair.Value,
        revision, commandId = "pickup-"+pair.Key, elapsedMs = 3_600_000 };
    var picked = Send(manual, request);
    Check((bool)picked["ok"]! && (string?)Order(picked,pair.Key)["stage"] == "ToDropoff", "Pickup did not depart.");
    var duplicate = Send(manual, request);
    Check((bool)duplicate["ok"]! && JToken.DeepEquals(picked["state"],duplicate["state"]), "Duplicate pickup changed state.");
    Check((bool)Send(restored,request)["ok"]!, "Restored pickup did not match original.");
}
var manualEnd = Send(manual, new { action = "tick", elapsedMs = 7_200_000 });
var replayEnd = Send(restored, new { action = "tick", elapsedMs = 7_200_000 });
Check((bool)manualEnd["state"]!["settled"]!, "Confirmed manual session did not settle.");
Check(JToken.DeepEquals(manualEnd["state"], replayEnd["state"]), "Replay event order/settlement differs.");
var events = (JArray)manualEnd["state"]!["events"]!;
Check(events.Count(value => (string?)value["code"] == "driver_pay_accrued") == 2, "Driver pay accrued more than once per order.");
Check(events.Count(value => (string?)value["code"] == "app_pickup_confirmed") == 2, "Pickup action recorded more than once per order.");
var damaged = JObject.Parse((string)saved["saveJson"]!);
damaged["inputHashSha256"]="wrong-input";
var beforeBadRestore = Snapshot(manual);
var badRestore = Send(manual, new { action = "restore", saveJson = damaged.ToString() });
Check(!(bool)badRestore["ok"]! && JToken.DeepEquals(beforeBadRestore["state"],badRestore["state"]), "Failed restore damaged running game.");
var exported = Send(manual,new { action="export" });
var export = JObject.Parse((string)exported["exportJson"]!);
Check((string?)export["schemaVersion"]=="game-business-preparation.v1", "Export schema changed.");
Check(!(bool)export["operationalActionsAllowed"]! && !(bool)export["approvedForTraining"]! && !(bool)export["weightsTrained"]!, "Export claims operational or training approval.");
Check(export["destinations"] is null && export["roads"] is null, "Export leaked destination/road payload.");

// A multi-day campaign must retain tick boundaries; one compressed jump can exceed the campaign
// input window or open an overnight round at a different virtual time.
var campaign = Create();
Check((bool)Send(campaign,new { action="campaign-start",sessionId="long-campaign-replay",manualPickup=false })["ok"]!, "Campaign start failed.");
const long policyGateMs=196_200_000;
Check((bool)Send(campaign,new { action="tick",elapsedMs=policyGateMs })["ok"]!, "Campaign policy gate failed.");
Check((bool)Send(campaign,new { action="campaign-policy",elapsedMs=policyGateMs,policy="chariot" })["ok"]!, "Campaign policy choice failed.");
for(var day=1; day<=5; day++)
    Check((bool)Send(campaign,new { action="tick",elapsedMs=policyGateMs+86_400_000L*day })["ok"]!, "Campaign daily tick failed.");
var campaignSaved=Send(campaign,new { action="save" });
var campaignRestored=Create();
var campaignReplay=Send(campaignRestored,new { action="restore",saveJson=(string)campaignSaved["saveJson"]! });
Check((bool)campaignReplay["ok"]! && JToken.DeepEquals(campaignSaved["state"],campaignReplay["state"]), "Long campaign replay lost tick boundaries.");
var changedCommands=JObject.Parse((string)campaignSaved["saveJson"]!);
var commandArray=(JArray)changedCommands["commands"]!;
var lastCommand=JObject.Parse((string)commandArray.Last!);
lastCommand["elapsedMs"]=(long)lastCommand["elapsedMs"]!+1000;
commandArray[commandArray.Count-1]=lastCommand.ToString(Formatting.None);
var beforeAltered=Snapshot(campaignRestored);
var alteredResult=Send(campaignRestored,new { action="restore",saveJson=changedCommands.ToString(Formatting.None) });
Check(!(bool)alteredResult["ok"]! && JToken.DeepEquals(beforeAltered["state"],alteredResult["state"]), "Altered replay result was accepted or damaged current game.");
Console.WriteLine(JsonConvert.SerializeObject(new { status="passed", assertions, legacyAutomaticParity=true,
    manualPickup=true, wrongDriverAndStaleRejected=true, duplicatePayProtected=true, replayEquivalent=true,
    settledClockFrozen=true, settledReplayPreservesBoundary=true,
    failedRestorePreservesState=true, longCampaignReplayEquivalent=true, finalReplayHashVerified=true,
    exportSimulationOnly=true }));
