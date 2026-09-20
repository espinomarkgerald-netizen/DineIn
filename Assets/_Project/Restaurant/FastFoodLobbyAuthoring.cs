using UnityEngine;

/// <summary>Editable Lobby2 links into the shared management and restock systems.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(FastFoodRestaurant))]
public sealed class FastFoodLobbyAuthoring : MonoBehaviour
{
    [SerializeField] private ManagementComputerStation roomComputer;
    [SerializeField] private RestockStockRoomEntrance dryStorageEntrance;
    [SerializeField] private RestockStockRoomEntrance freezerEntrance;
    [SerializeField] private Transform cashierHome;
    [SerializeField] private Transform busserHome;
    [SerializeField] private SinkInteractable sink;
    public ManagementComputerStation RoomComputer => roomComputer;
    public RestockStockRoomEntrance DryStorageEntrance => dryStorageEntrance;
    public RestockStockRoomEntrance FreezerEntrance => freezerEntrance;
    public Transform CashierHome => cashierHome;
    public Transform BusserHome => busserHome;
    public SinkInteractable Sink => sink;

    public bool ValidateAuthoring(out string problem)
    {
        problem = null;
        if (roomComputer == null || dryStorageEntrance == null || freezerEntrance == null ||
            cashierHome == null || busserHome == null || sink == null)
            problem = "Assign the room computer, two storage entrances, staff home points and sink.";
        else if (dryStorageEntrance == freezerEntrance || dryStorageEntrance.StorageType != RestockStorageType.Dry ||
                 freezerEntrance.StorageType != RestockStorageType.Frozen)
            problem = "Use separate Dry and Frozen entrances into the existing RestockScene.";
        else foreach (var entrance in new[] { dryStorageEntrance, freezerEntrance })
        {
            if (entrance.gameObject.scene != gameObject.scene || entrance.StandPoint == entrance.transform ||
                entrance.GetComponent<Collider>() == null || entrance.GetComponent<Outline>() == null)
                problem = "Each storage entrance needs an authored stand point, collider and outline in Lobby2.";
        }
        return problem == null;
    }
}
