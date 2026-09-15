using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
[DefaultExecutionOrder(-90)]
public class MultiplayerManagerRegistration : MonoBehaviourPunCallbacks, IOnPhotonViewPreNetDestroy
{
    private MultiplayerSessionManager session;
    private int actorNumber;

    private void Start()
    {
        photonView.AddCallbackTarget(this);
        RegisterManager();
    }

    public override void OnJoinedRoom() => RegisterManager();

    private void RegisterManager()
    {
        // PUN has assigned the owner by Start, for both local and remote instances.
        session = MultiplayerSessionManager.Instance;
        actorNumber = photonView.OwnerActorNr;
        if (session != null) session.Register(this);
    }

    private void OnDestroy()
    {
        photonView.RemoveCallbackTarget(this);
        if (session != null) session.Unregister(actorNumber, this);
    }

    public void OnPreNetDestroy(PhotonView rootView)
    {
        if (rootView != photonView || session == null || !session.IsAuthority
            || actorNumber == session.LocalActorNumber || session.ValidActor(actorNumber)) return;
        // PUN is about to remove a departed avatar. Preserve shared items before
        // Unity destroys its children; the host's normal recovery releases them.
        foreach (var tray in GetComponentsInChildren<FoodTray>(true)) tray.transform.SetParent(null, true);
        foreach (var bag in GetComponentsInChildren<TakeoutBagInteractable>(true)) bag.transform.SetParent(null, true);
        foreach (var money in GetComponentsInChildren<MoneyPickup>(true)) money.transform.SetParent(null, true);
        foreach (var bill in GetComponentsInChildren<BillPaper>(true))
            BillManager.Instance?.ReturnUndeliveredBill(bill.TargetGroup);
    }
}
