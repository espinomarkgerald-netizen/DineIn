using UnityEngine;

public class UIButtonMove : MonoBehaviour
{
    public SimplePlayerMovement player;
    public Transform Terminal;
    public Transform Board;
    public GameObject TerminalUI;
    public GameObject BoardUI;

    public void MoveToTerminal()
    {
        player.MoveToTargetAndShowUI(Terminal, TerminalUI);
    }

    public void MoveToBoard()
    {
        player.MoveToTargetAndShowUI(Board, BoardUI);
    }
}