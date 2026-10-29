using Stikling.Core.Models;

namespace Stikling.Core.Timeline;

public static partial class HistoryText
{
    // Every line template, with what each value in it is. The more specific template of two that could read the same
    // English text comes first, since Parse takes the first that fits. Each key has its wording in Resources/History.resx.
    private static List<Template> Build()
    {
        var all = new List<Template>();

        static Arg Txt() => new(Kind.Text);
        static Arg Num() => new(Kind.Number);
        static Arg Mo() => new(Kind.Month);
        static Arg En(Type type) => new(Kind.Enum, type);
        static Arg EnLower(Type type) => new(Kind.EnumLower, type);
        static Arg EnLowerFirst(Type type) => new(Kind.EnumLowerFirst, type);
        static Arg Lst(Type type) => new(Kind.List, type);
        static Arg LstSentence(Type type) => new(Kind.ListSentence, type);
        static Arg LstLowerFirst(Type type) => new(Kind.ListLowerFirst, type);

        void Add(string key, params Arg[] args) => all.Add(new Template(key, args, -1, true));
        void NoParse(string key, params Arg[] args) => all.Add(new Template(key, args, -1, false));
        void Plural(string key, int countArg, params Arg[] args) => all.Add(new Template(key, args, countArg, true));

        Add("StatusChanged", En(typeof(PlantStatus)), En(typeof(PlantStatus)));
        Add("WhatHappened", Txt());
        Add("DepartureSoldToFor", Txt(), Txt());
        Add("DepartureSoldTo", Txt());
        Add("DepartureSoldFor", Txt());
        Add("DepartureSwappedFor", Txt());
        Add("DepartureGivenToSwapped", Txt(), Txt());
        Add("DepartureGivenTo", Txt());
        Add("RemovedFromRoom");
        Add("RemovedFrom", Txt());
        Add("PlacedIn", Txt());
        Add("MovedTo", Txt());
        Add("MovedFromTo", Txt(), Txt());
        Add("MediumChanged", En(typeof(GrowingMedium)), En(typeof(GrowingMedium)));
        Add("HowManyChanged", Num(), Num());
        Add("LightChangedFromUnset", En(typeof(LightLevel)));
        Add("LightChanged", En(typeof(LightLevel)), En(typeof(LightLevel)));
        Add("LightCleared");
        Add("PottedInto", Txt());
        Add("TakenOutOfPot");
        Add("NowStandsIn", Txt());
        Add("OuterPotRemoved");
        Add("NowInMix", Txt());
        Add("MixRemoved");
        Add("FedStopped");
        Add("FedEverySecond", Txt());
        Add("FedEveryThird", Txt());
        Add("FedEveryWatering", Txt());
        Add("PotWentWithIt");
        Add("OuterPotWentWithIt");
        Add("PotsWentWithIt");
        Add("PotsStayed");
        Add("WateredInOuterPot");
        Add("NotWateredInOuterPot");
        Add("Tagged", Txt());
        Add("Untagged", Txt());
        Add("PutInQuarantine");
        Add("OutOfQuarantine");
        Add("WentDormant");
        Add("WokeUp");
        Add("StartedMoveToSemiHydro");
        Add("StartedMoveTo", En(typeof(GrowingMedium)));
        Add("RootsWashed");
        Add("FirstWaterRoot");
        Add("FirstNewLeaf");
        Plural("SettledInSemiHydro", 0, Num());
        Plural("SettledIn", 1, En(typeof(GrowingMedium)), Num());
        Add("StoppedTrackingMoveSemiHydro");
        Add("StoppedTrackingMove", En(typeof(GrowingMedium)));
        Add("StageChanged", En(typeof(PropagationStage)), En(typeof(PropagationStage)));
        Add("CountChanged", Num(), Num());
        Add("PottedUpChanged", Num(), Num());
        Add("GivenAwayChanged", Num(), Num());
        Add("FailedChanged", Num(), Num());
        Add("NewSetup", Txt());
        Add("FirstRootOnDay", Num());
        Add("FirstRootCleared");
        Add("FirstLeafOnDay", Num());
        Add("FirstLeafCleared");
        Add("SeedsCameUp", Num(), Num());
        Add("SeedCountCleared");
        Add("CormSize", Num());
        Add("CormSizeCleared");
        Add("NoRootingAids");
        Add("RootingAids", Lst(typeof(RootingAid)));
        Plural("CutEndDriedDays", 0, Num());
        Plural("CutEndDriedHours", 0, Num());
        Add("CutEndNotDried");
        Add("AddedLivesElsewhere");
        Add("AddedToCollectionMonth", Mo());
        Add("AddedToCollectionYear", Num());
        Add("AddedToCollection");
        Add("AddedPropagatedMonth", Mo());
        Add("AddedPropagatedYear", Num());
        Add("AddedPropagated");
        Add("SplitOffFrom", Txt());
        Add("PottedUpFromPropagation");
        Add("IdentifiedAs", Txt());
        Add("PottedTogetherWith", Txt());
        Add("PottedInWithIt", Txt(), Num());
        Add("TakenOutOfSharedPot");
        Add("TakenOutOfPotOf", Txt());
        Add("PlantTakenOut", Txt(), Num());
        Add("SomeDied", Num(), Num());
        Add("GaveSomeAway", Num(), Num());
        Add("GaveSomeTo", Num(), Txt(), Num());
        Add("PottedUpOnTheirOwn", Num(), Txt());
        Add("Started", Num(), EnLower(typeof(PropagationType)), En(typeof(GrowingMedium)));
        Add("BatchOf", Num(), EnLower(typeof(PropagationType)), En(typeof(GrowingMedium)));
        Add("PottedUp", Num(), Txt());
        Add("FailedWithReasonNote", Num(), En(typeof(FailReason)), Txt());
        Add("FailedWithReason", Num(), En(typeof(FailReason)));
        Add("FailedWithNote", Num(), Txt());
        Add("Failed", Num());
        Add("FinishedAll", Num(), Num(), Num());
        Add("FinishedPottedGiven", Num(), Num());
        Add("FinishedPottedFailed", Num(), Num());
        Add("FinishedGivenFailed", Num(), Num());
        Add("FinishedPotted", Num());
        Add("FinishedGiven", Num());
        Add("FinishedFailed", Num());
        Add("FinishedNothing");
        Add("GaveToWho", Num(), Txt());
        Add("GaveAway", Num());
        Add("FloweringBuds");
        Add("FloweringInFlower");
        Add("FloweringDone");
        Add("ProblemOpened", LstLowerFirst(typeof(ProblemSign)));
        Add("ProblemUpdateTrendActions", En(typeof(ProblemTrend)), LstSentence(typeof(ProblemAction)));
        Add("ProblemUpdateTrend", En(typeof(ProblemTrend)));
        Add("ProblemUpdateActions", LstSentence(typeof(ProblemAction)));
        Add("ProblemUpdateEmpty");
        Add("ProblemClosed", EnLowerFirst(typeof(ProblemOutcome)));
        Add("ProblemReopened");
        Add("LookedOver");
        Add("ReceivedFrom", Txt());
        NoParse("TextLine", Txt());

        return all;
    }
}
