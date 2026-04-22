using UnityEngine;

public class BlackBoardHandler : MonoBehaviour
{
    private BlackBoard blackBoard = new BlackBoard();
    private Initializable[] initializables;

    private void Awake()
    {
        initializables = GetComponentsInChildren<Initializable>();
    }

    public BlackBoard GetBlackBoard()
    {
        return blackBoard;
    }

    public void Initialized()
    {
        if (null != initializables)
        {
            foreach (var i in initializables)
            {
                i.Initialize(blackBoard);
            }
        }
    }
}
