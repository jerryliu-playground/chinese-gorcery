using System.Text.Json.Serialization;

namespace ChineseGrocery.Sim.Tasks;

/// <summary>A unit of work on the shared task board. Staff claim tasks automatically; the player claims them by interacting.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$task")]
[JsonDerivedType(typeof(RestockTask), "restock")]
[JsonDerivedType(typeof(CheckoutTask), "checkout")]
[JsonDerivedType(typeof(CutMeatTask), "cut_meat")]
[JsonDerivedType(typeof(TidyTask), "tidy")]
[JsonDerivedType(typeof(CleanTankTask), "clean_tank")]
[JsonDerivedType(typeof(RemoveDeadTask), "remove_dead")]
[JsonDerivedType(typeof(KillFishTask), "kill_fish")]
[JsonDerivedType(typeof(CookTask), "cook")]
[JsonDerivedType(typeof(ServeDeliTask), "serve_deli")]
[JsonDerivedType(typeof(UnloadTask), "unload")]
public abstract record SimTask(int Id, TaskType Type, int Priority);
