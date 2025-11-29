using UnityEngine;
using UnityEngine.UI;

public class GameResultUI : MonoBehaviour
{
    [SerializeField] private Button retryBtn;
    [SerializeField] private Button quitBtn;

    void Start()
    {
        retryBtn.onClick.AddListener(() =>
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        });

        quitBtn.onClick.AddListener(() =>
        {
            Application.Quit();
        });
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
