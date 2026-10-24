using System.Text.Json.Serialization;
using ChineseGrocery.Sim.Economy;
using ChineseGrocery.Sim.Grid;
using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Staff;
using ChineseGrocery.Sim.Tasks;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Core;

/// <summary>A request from the view layer. Queued, then applied at the next tick boundary (and saved while queued).</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$command")]
[JsonDerivedType(typeof(OpenStore), "open_store")]
[JsonDerivedType(typeof(StartNextDay), "start_next_day")]
[JsonDerivedType(typeof(PlaceOrder), "place_order")]
[JsonDerivedType(typeof(PlaceFixture), "place_fixture")]
[JsonDerivedType(typeof(MoveFixture), "move_fixture")]
[JsonDerivedType(typeof(SellFixture), "sell_fixture")]
[JsonDerivedType(typeof(AssignShelfSlot), "assign_shelf_slot")]
[JsonDerivedType(typeof(SetPrice), "set_price")]
[JsonDerivedType(typeof(MovePlayer), "move_player")]
[JsonDerivedType(typeof(PlayerInteract), "player_interact")]
[JsonDerivedType(typeof(UpgradeStage), "upgrade_stage")]
[JsonDerivedType(typeof(HireEmployee), "hire_employee")]
[JsonDerivedType(typeof(SetWage), "set_wage")]
public abstract record SimCommand;
