// A scene binding for the existing gameplay systems, shared session transport stays restaurant-independent.
public interface IMultiplayerSessionGameplay
{
    bool ReadyForInput { get; }
    bool RestorationComplete { get; }
    void InitializeSession();
    void RequestRestoration();
    void ParticipantReturned();
    void ConnectionLost();
    void SuspendSession(bool ended);
}
