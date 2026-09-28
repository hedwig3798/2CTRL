using UnityEngine;

public class BlackBoardHandler 
    : MonoBehaviour
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

    public void Initialize()
    {
        if (null == initializables)
        {
            initializables = GetComponentsInChildren<Initializable>(true);
        }

        foreach (var i in initializables)
        {
            if (null != i)
            {
                i.Initialize(blackBoard);
            }
        }
    }
}
