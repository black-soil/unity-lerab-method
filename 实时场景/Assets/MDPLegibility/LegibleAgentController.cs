using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LegibleAgentController : MonoBehaviour
{
    [Header("核心引用")]
    [SerializeField] public GridMDP_5x8 grid;
    [SerializeField] public MDPLegibilityPlanner planner;
    [SerializeField] public BayesianGoalObserver observer;

    [Header("智能体设置")]
    [SerializeField] private int startState = 16;  // 起始状态
    [SerializeField] private int currentState = 16; // 当前状态

    [Header("移动参数")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float rotationSpeed = 180f;
    [SerializeField] private float snapThreshold = 0.075f;

    [Header("观测特征源")]
    [SerializeField] private Renderer visualIndicator;
    [SerializeField] private string emissionProperty = "_EmissionIntensity";

    [Header("运行时状态")]
    [SerializeField] private bool autoCruise = false;
    [SerializeField] private float stepInterval = 0.68f;

    [Header("视觉反馈")]
    [SerializeField] private AgentVisualFeedback visualFeedback;




    // 8 方向向量（与你 Python 动作顺序对齐：0↑ 1↓ 2← 3→ 4↖ 5↗ 6↙ 7↘）
    private readonly Vector3[] DirectionVectors =
    {
        Vector3.forward,          // 0 ↑
        Vector3.back,             // 1 ↓
        Vector3.left,             // 2 ←
        Vector3.right,            // 3 →
        Vector3.forward + Vector3.left,   // 4 ↖
        Vector3.forward + Vector3.right,  // 5 ↗
        Vector3.back + Vector3.left,      // 6 ↙
        Vector3.back + Vector3.right      // 7 ↘
    };



    private Rigidbody rb;
    private Coroutine moveCoroutine;
    private List<int> trajectory = new List<int>();

    void Start()
    {
        InitializeComponents();
        ResetAgent();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            TriggerStep();
    }

    private void InitializeComponents()
    {
        if (!rb) rb = GetComponent<Rigidbody>() ?? gameObject.AddComponent<Rigidbody>();
        if (!grid) grid = FindObjectOfType<GridMDP_5x8>();
        if (!planner) planner = FindObjectOfType<MDPLegibilityPlanner>();
        if (!observer) observer = FindObjectOfType<BayesianGoalObserver>();

        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        rb.useGravity = false;
    }

    [ContextMenu("重置智能体")]
    public void ResetAgent()
{
    StopAllCoroutines();
    
    // 使用 startState 而不是 currentState
    currentState = startState;
    
    if (grid)
    {
        transform.position = grid.StateToWorldPos(currentState);
    }
    else
    {
        Debug.LogWarning("Grid reference missing, using default position.");
        transform.position = new Vector3(0, 0.5f, 0);
    }
    
    transform.rotation = Quaternion.identity;
    
    if (observer != null)
    {
        observer.ResetBeliefs();
    }
    else
    {
        Debug.LogWarning("Observer 未绑定，跳过重置信念");
    }
    
    trajectory.Clear();
    trajectory.Add(currentState);
    Debug.Log($"🚀 Agent 重置到状态{currentState}");
}
    [ContextMenu("触发单步移动")]
    public void TriggerStep()
    {
        if (moveCoroutine != null) return;
        moveCoroutine = StartCoroutine(ExecuteStep());
    }

    public int GetCurrentState() 
    { 
        return currentState; 
    }
    private IEnumerator ExecuteStep()
    {
        // 1. 查表选动作
        int action = planner.LegiblePolicy[currentState];
        Vector3 targetDir = DirectionVectors[action].normalized;

        // 2. 采样下一状态（离散层）
        int nextState = SampleNextState(currentState, action);
        if (nextState == currentState) 
        {
            Debug.Log($"[撞墙/边界] 状态{currentState} 动作{action} → 停留在{nextState}");
            moveCoroutine = null;
            yield break;
        }

        // 3. 执行连续移动（平滑旋转+位移）
        Vector3 startPos = transform.position;
        Vector3 targetPos = grid.StateToWorldPos(nextState);
        float duration = Vector3.Distance(startPos, targetPos) / moveSpeed;

        // 旋转对准目标方向
        Quaternion startRot = transform.rotation;
        Quaternion targetRot = Quaternion.LookRotation(targetDir);
        Debug.Log($"旋转: 从 {startRot.eulerAngles} 到 {targetRot.eulerAngles}");

        float rotTime = 0.15f;
        for (float t = 0; t < rotTime; t += Time.deltaTime)
        {
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t / rotTime);
            yield return null;
        }

        // 平移
        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(startPos, targetPos, t / duration);
            yield return null;
        }
        transform.position = targetPos; // 强制贴合

        // 4. 采集实时观测值
        Vector3 forward = transform.forward;
        float speed = moveSpeed; // 匀速移动
        float colorIntensity = ReadColorIntensity();

        // 5. 贝叶斯更新
        observer.UpdateBelief(currentState, action, nextState, forward, speed, colorIntensity);
        observer.PrintCurrentBeliefs();

        // 6. 触发视觉更新
        if (visualFeedback)
        visualFeedback.SetColorIntensity(observer.Beliefs[planner.assumedTrueGoal]); // 示例：用目标0的信念

        // 7. 更新状态与轨迹
        trajectory.Add(nextState);
        currentState = nextState;

        Debug.Log($"🔄 完成：{currentState} → {nextState} (动作{action})，轨迹长度={trajectory.Count}");
        moveCoroutine = null;

        // 自动巡航模式
        if (autoCruise && !grid.IsGoal(currentState))
            Invoke(nameof(TriggerStep), stepInterval);
    }

    private int SampleNextState(int state, int action)
    {
        // 简版：取最大概率转移（与你 Python 的 deterministic 近似）
        float maxProb = 0;
        int candidate = state;
        for (int ns = 0; ns < GridMDP_5x8.NUM_STATES; ns++)
        {
            float p = grid.Transition[state, action, ns];
            if (p > maxProb)
            {
                maxProb = p;
                candidate = ns;
            }
        }
        return candidate;
    }

    private float ReadColorIntensity()
    {
        if (!visualIndicator || !visualIndicator.material) return 127f;
        
        try
        {
            // 尝试读取 Emission 强度，映射到 0-255
            float val = visualIndicator.material.GetFloat(emissionProperty);
            return Mathf.Clamp(val * 155f + 100f, 0, 255);
        }
        catch
        {
            return 140f; // 降级默认值
        }
    }

    [ContextMenu("开启自动巡航")]
    public void StartAutoCruise() => autoCruise = true;

    [ContextMenu("停止自动巡航")]
    public void StopAutoCruise() => autoCruise = false;

    // 调试工具：打印当前轨迹
    [ContextMenu("打印轨迹")]
    public void PrintTrajectory()
    {
        string path = string.Join("→", trajectory);
        Debug.Log($"📋 轨迹: {path}");
    }
}