using System.Text.Json.Serialization;

namespace ChineseGrocery.Sim.Tasks;

/// <summary>A unit of work on the shared task board. Staff claim tasks automatically; the player claims them by interacting.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$task")]
[JsonDerivedType(typeof(RestockTask), "restock")]
[JsonDerivedType(typeof(CheckoutTask), "checkout")]
[JsonDerivedType(typeof(CutMeatTask), "cut_meat")]
public abstract record SimTask(int Id, TaskType Type, int Priority);
