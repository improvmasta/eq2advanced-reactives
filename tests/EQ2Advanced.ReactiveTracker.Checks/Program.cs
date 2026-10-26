using EQ2Advanced.ReactiveTracker.Core;

static void Check(bool condition, string label)
{
    if (!condition) throw new Exception(label);
}

const long t = 1_785_630_900;
var inq = new ReactiveEngine("Ipax");
Check(inq.Accept("You demand retribution for any wrongs done to YOU.", t), "single cast recognized");
Check(inq.Snapshot(t).Recipients.Single().Single?.Remaining == 5, "single begins with five triggers");
Check(inq.Accept("YOUR Vengeful Faith heals Ipax for a critical of 452 hit points.", t + 1), "single proc recognized");
Check(inq.Snapshot(t + 1).Recipients.Single().Single?.Remaining == 4, "single proc consumes one trigger");
inq.Accept("You demand retribution for any wrongs done to Ipax.", t + 2);
Check(inq.Snapshot(t + 2).Recipients.Single().Single?.Remaining == 5, "recast replaces count");
inq.Accept("You feel the strong prayerful protection dissipate.", t + 3);
Check(inq.Snapshot(t + 3).Recipients.Count == 0, "explicit single expiry removes effect");

inq.Accept("You raise your voice in a Malevolent Diatribe.", t + 10);
Check(inq.Snapshot(t + 10).Recipients.Count == 0, "cast speech alone does not claim protected recipients");
inq.Accept("Ipax's group is protected by a Malevolent Diatribe!", t + 10);
inq.Accept("Birch's group is protected by a Malevolent Diatribe!", t + 10);
inq.Accept("Spades' group is protected by a Malevolent Diatribe!", t + 10);
Check(inq.Snapshot(t + 10).Recipients.Count == 3, "protection lines supply group roster, including possessive names ending in s");
Check(inq.Snapshot(t + 10).Recipients.Single(row => row.Name == "Birch").Group?.Remaining == 3, "recipient has group triggers");
inq.Accept("YOUR Atoning Faith heals Birch for 315 hit points.", t + 11);
Check(inq.Snapshot(t + 11).Recipients.Single(row => row.Name == "Birch").Group?.Remaining == 2, "group proc consumes only its recipient");
Check(inq.Snapshot(t + 11).Recipients.Single(row => row.Name == "Ipax").Group?.Remaining == 3, "other recipient unaffected");
inq.Accept("Birch's group is no longer protected by a Malevolent Diatribe.", t + 12);
Check(inq.Snapshot(t + 12).Recipients.Single(row => row.Name == "Birch").Group is null, "overwrite or dispel clears that recipient");
inq.Accept("You raise your voice in a Malevolent Diatribe.", t + 13);
inq.Accept("Birch's group is protected by a Malevolent Diatribe!", t + 13);
Check(inq.Snapshot(t + 13).Recipients.Single(row => row.Name == "Birch").Group?.Remaining == 3, "new protection refreshes after overwrite");
inq.Accept("Klebb has joined the group.", t + 14);
Check(inq.Snapshot(t + 14).Recipients.Any(row => row.Name == "Klebb" && row.Group is null), "join appears in roster without invented count");
inq.Accept("Klebb left the group.", t + 15);
Check(inq.Snapshot(t + 15).Recipients.All(row => row.Name != "Klebb"), "departure removes group member");
inq.Accept("Alas, Birch has died from pain and suffering.", t + 16);
Check(inq.Snapshot(t + 16).Recipients.Single(row => row.Name == "Birch").Group is null, "tracked player's death clears effects");
inq.Accept("You have left the group.", t + 17);
Check(inq.Snapshot(t + 17).Recipients.All(row => row.Name == "Ipax"), "local group departure clears former members");

var temp = new ReactiveEngine("Avacii") { Options = new HealerOptions { CoerciveHealing = true, TemplarEofTwoSet = true } };
temp.Accept("You pray for YOU's body and soul.", t);
Check(temp.Snapshot(t).Recipients.Single().Single?.Remaining == 10, "Templar set and Coercive Healing bonuses stack");
temp.Accept("You pray devoutly for Holy Intercession.", t + 2);
temp.Accept("Your group is protected by a Holy Intercession!", t + 2);
Check(temp.Snapshot(t + 2).Recipients.Single().Group?.Remaining == 4, "group bonus applies to new casts");
Check(temp.Snapshot(t + 31).Recipients.All(row => row.Single is null), "single effect expires after thirty seconds");
Check(temp.Snapshot(t + 33).Recipients.All(row => row.Group is null), "group effect expires after thirty seconds");

var unknown = new ReactiveEngine("Avacii");
unknown.Accept("YOUR Divine Prayer heals Birch for 200 hit points.", t);
Check(unknown.Snapshot(t).Recipients.Count == 0, "unseen cast never invents a remaining count");
Check(unknown.LastObservation.Contains("unknown", StringComparison.Ordinal), "unseen cast explains uncertainty");
Check(inq.Snapshot(t + 18).Character == "Ipax" && temp.Snapshot(t + 18).Character == "Avacii", "boxed healer state stays separate");
Console.WriteLine("Reactives checks passed: single and group casts, procs, overwrite, expiry, membership, death, bonuses, unknown state.");
