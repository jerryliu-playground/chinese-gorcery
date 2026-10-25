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
[JsonDerivedType(typeof(RestockAll), "restock_all")]
[JsonDerivedType(typeof(Deli.SetMenuItem), "set_menu_item")]
[JsonDerivedType(typeof(HireApplicant), "hire_applicant")]
[JsonDerivedType(typeof(RejectApplicant), "reject_applicant")]
[JsonDerivedType(typeof(FireEmployee), "fire_employee")]
[JsonDerivedType(typeof(SetShift), "set_shift")]
[JsonDerivedType(typeof(SetRestDays), "set_rest_days")]
[JsonDerivedType(typeof(SetWorkerTasks), "set_worker_tasks")]
[JsonDerivedType(typeof(SetTaskPriority), "set_task_priority")]
[JsonDerivedType(typeof(AssignManager), "assign_manager")]
[JsonDerivedType(typeof(SetAutomation), "set_automation")]
[JsonDerivedType(typeof(SetOrderTarget), "set_order_target")]
[JsonDerivedType(typeof(Imports.PlaceImportOrder), "place_import_order")]
[JsonDerivedType(typeof(Imports.UpgradeStorage), "upgrade_storage")]
[JsonDerivedType(typeof(Reputation.AcceptGroupOrder), "accept_group_order")]
[JsonDerivedType(typeof(Reputation.DeclineGroupOrder), "decline_group_order")]
[JsonDerivedType(typeof(Reputation.BuyBilingualLabels), "buy_bilingual_labels")]
public abstract record SimCommand;
