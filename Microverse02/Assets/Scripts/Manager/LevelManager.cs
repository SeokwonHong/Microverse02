using UnityEngine;

public class LevelManager : MonoBehaviour
{

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
            GameManager.Instance.OnLevelCompleted();
        }
        if(Input.GetKeyDown(KeyCode.R))
        {

            GameManager.Instance.OnLevelRestart();
        }
    }
}
