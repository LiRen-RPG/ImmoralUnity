using UnityEngine;

/// <summary>横板通关 - 关卡终点</summary>
public class PlatformerLevelGoal : MonoBehaviour
{
    [Header("通关提示")]
    public string winMessage = "通关成功！";
    private bool reached = false;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (reached) return;
        if (other.CompareTag("Player"))
        {
            reached = true;
            Debug.Log(winMessage);
            // 可在此处加载下一场景：UnityEngine.SceneManagement.SceneManager.LoadScene(...)
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.84f, 0f, 0.5f);
        Gizmos.DrawCube(transform.position, new Vector3(1f, 2.5f, 0.1f));
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(1f, 2.5f, 0.1f));
    }
}
