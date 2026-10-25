using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Story;

public sealed record TutorialStepCompleted(int Step) : SimEvent;
