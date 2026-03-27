using UnityEngine;

public class UIButtonMove : MonoBehaviour
{
    [Header("Player Reference")]
    public SimplePlayerMovement player;

    [Header("Targets")]
    public Transform Terminal;
    public Transform Board;

    // Called by UI Button
    public void MoveToTerminal()
    {
        if (player != null && Terminal != null)
            player.MoveToTarget(Terminal.position);
    }

    // Called by UI Button
    public void MoveToBoard()
    {
        if (player != null && Board != null)
            player.MoveToTarget(Board.position);
    }
}