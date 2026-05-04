using UnityEngine;

public class AgentVisualFeedback : MonoBehaviour
{
    [Header("视觉组件")]
    [SerializeField] private MeshRenderer bodyRenderer;
    [SerializeField] private Transform arrowIndicator; // 指向箭头的子物体（可选）

    [Header("颜色映射")]
    [SerializeField] private Gradient colorGradient; // 蓝色(0.0) → 绿色(0.5) → 红色(1.0)
    [SerializeField] private string colorIntensityProperty = "_EmissionIntensity";

    [Header("朝向可视化")]
    [SerializeField] private float arrowHeight = 1.2f;
    [SerializeField] private bool enableDebug = true; // 新增：调试开关
    
    private Material agentMaterial;
    private LegibleAgentController agentController;
    private BayesianGoalObserver observer;
    private Vector3 lastMoveDir = Vector3.zero; // 用于检测方向变化

    void Start()
    {
        agentController = GetComponent<LegibleAgentController>();
        observer = FindObjectOfType<BayesianGoalObserver>();
        
        if (bodyRenderer)
        {
            agentMaterial = bodyRenderer.material;
            UpdateVisualColor(0.3f);
        }
        
        if (!arrowIndicator)
        {
            CreateArrowIndicator();
        }
        
        if (enableDebug)
            Debug.Log($"🔧 AgentVisualFeedback 初始化完成");
    }

    private void CreateArrowIndicator()
    {
        // 创建空物体作为箭头容器
        GameObject arrowObj = new GameObject("DirectionArrow");
        arrowIndicator = arrowObj.transform;
        arrowIndicator.SetParent(transform);
        arrowIndicator.localPosition = Vector3.up * arrowHeight;
        
        // 创建箭头身体（细长立方体）
        GameObject arrowBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
        arrowBody.transform.SetParent(arrowIndicator);
        arrowBody.transform.localScale = new Vector3(0.1f, 0.1f, 0.3f);
        arrowBody.transform.localPosition = new Vector3(0, 0, 0.15f);
        arrowBody.GetComponent<MeshRenderer>().material.color = Color.yellow;
        
        // 创建箭头头部
        GameObject arrowHead = GameObject.CreatePrimitive(PrimitiveType.Cube);
        arrowHead.transform.SetParent(arrowIndicator);
        arrowHead.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
        arrowHead.transform.localPosition = new Vector3(0, 0, 0.3f);
        arrowHead.GetComponent<MeshRenderer>().material.color = Color.yellow;
        
        // // 旋转整个箭头
        // arrowIndicator.localRotation = Quaternion.Euler(90f, 0f, 0f);
        
        if (enableDebug)
            Debug.Log($"🔄 箭头创建完成，初始旋转: {arrowIndicator.localRotation.eulerAngles}");
            Debug.Log($"✅ 箭头创建完成，位置: {arrowIndicator.localPosition}, 旋转: {arrowIndicator.localRotation.eulerAngles}");

    }

    void Update()
    {
        if (!agentController || !observer) return;
        
        UpdateColorByBelief();
        UpdateDirectionArrow();
    }

    private void UpdateColorByBelief()
    {
        if (!agentMaterial || observer.Beliefs == null) return;
        
        float maxBelief = 0;
        int maxIdx = 0;
        for (int i = 0; i < observer.Beliefs.Length; i++)
        {
            if (observer.Beliefs[i] > maxBelief)
            {
                maxBelief = observer.Beliefs[i];
                maxIdx = i;
            }
        }
        
        float beliefNorm = Mathf.Clamp01((maxBelief - 0.25f) / 0.75f);
        UpdateVisualColor(beliefNorm);
    }


    private void UpdateVisualColor(float intensity)
    {
        if (!agentMaterial) return;
        
        Color targetColor = colorGradient.Evaluate(intensity);
        agentMaterial.color = targetColor;
        agentMaterial.SetFloat(colorIntensityProperty, intensity * 2f);
        agentMaterial.SetColor("_EmissionColor", targetColor * intensity);
    }

    // private void UpdateDirectionArrow()
    // {
    //     if (!arrowIndicator) return;
        
    //     if (arrowIndicator == transform)
    //     {
    //         Debug.LogWarning("ArrowIndicator不应设置为Agent自身，请使用子物体");
    //         return;
    //     }
        
    //     arrowIndicator.localPosition = Vector3.up * arrowHeight;
        
    //     // 获取智能体当前移动方向
    //     Vector3 moveDir = transform.forward;
        
    //     // 检查方向是否变化
    //     if (Vector3.Angle(moveDir, lastMoveDir) > 5f) // 变化超过5度
    //     {
    //         if (enableDebug)
    //         {
    //             Debug.Log($"🧭 方向变化: {Vector3.Angle(moveDir, lastMoveDir):F1}°");
    //             Debug.Log($"  智能体朝向: {moveDir.x:F2}, {moveDir.y:F2}, {moveDir.z:F2}");
    //             Debug.Log($"  箭头旋转: {arrowIndicator.rotation.eulerAngles}");
    //             Debug.Log($"  箭头局部旋转: {arrowIndicator.localRotation.eulerAngles}");
                
    //             // 计算方向对应8个动作的哪个
    //             int action = GetClosestAction(moveDir);
    //             Debug.Log($"  最近动作编号: {action} ({GetActionName(action)})");
    //         }
    //         lastMoveDir = moveDir;
    //     }
        
    //     if (moveDir.sqrMagnitude > 0.1f)
    //     {
    //         arrowIndicator.rotation = Quaternion.LookRotation(moveDir);
    //     }
    // }
    
    private void UpdateDirectionArrow()
    {
        if (!arrowIndicator) return;
        
        if (arrowIndicator == transform)
        {
            Debug.LogWarning("ArrowIndicator不应设置为Agent自身，请使用子物体");
            return;
        }
        
        // 箭头位置保持在头顶
        arrowIndicator.localPosition = Vector3.up * arrowHeight;
        
        // ==== 核心修改：让箭头指向最终目标，而不是智能体当前朝向 ====
        // 1. 获取当前假设的真实目标状态
        if (agentController == null || agentController.planner == null || agentController.grid == null)
        {
            Debug.LogWarning("无法获取目标信息，箭头功能禁用。");
            return;
        }
        
        int goalIndex = agentController.planner.assumedTrueGoal;
        int goalState = agentController.grid.GOAL_STATES[goalIndex];
        
        // 2. 获取目标的世界坐标
        Vector3 goalWorldPos = agentController.grid.StateToWorldPos(goalState);
        
        // 3. 计算从箭头位置指向目标的方向向量
        // 注意：使用世界坐标计算方向
        Vector3 toGoalDir = goalWorldPos - arrowIndicator.position;
        toGoalDir.y = 0; // 可选：在XZ平面投影，保持箭头水平
        
        // 4. 应用旋转，使箭头指向目标
        if (toGoalDir.sqrMagnitude > 0.1f)
        {
            arrowIndicator.rotation = Quaternion.LookRotation(toGoalDir.normalized);
            
            // 可选：添加调试信息
            if (enableDebug && Time.frameCount % 60 == 0) // 每秒打印一次
            {
                Debug.Log($"🎯 箭头指向目标{goalIndex}(状态{goalState})，方向: {toGoalDir.normalized}");
            }
        }
    }
    // 调试方法：获取最接近的动作编号
    private int GetClosestAction(Vector3 direction)
    {
        Vector3[] actionVectors = {
            Vector3.forward,    // 0 ↑
            Vector3.back,       // 1 ↓
            Vector3.left,       // 2 ←
            Vector3.right,      // 3 →
            Vector3.forward + Vector3.left,   // 4 ↖
            Vector3.forward + Vector3.right,  // 5 ↗
            Vector3.back + Vector3.left,      // 6 ↙
            Vector3.back + Vector3.right      // 7 ↘
        };
        
        int bestAction = 0;
        float bestDot = -1f;
        
        for (int i = 0; i < actionVectors.Length; i++)
        {
            float dot = Vector3.Dot(direction.normalized, actionVectors[i].normalized);
            if (dot > bestDot)
            {
                bestDot = dot;
                bestAction = i;
            }
        }
        
        return bestAction;
    }
    
    private string GetActionName(int action)
    {
        string[] actionNames = { "↑", "↓", "←", "→", "↖", "↗", "↙", "↘" };
        return action < actionNames.Length ? actionNames[action] : "未知";
    }

    [ContextMenu("手动测试箭头指向")]
    public void DebugArrowOrientation()
    {
        if (!arrowIndicator)
        {
            Debug.LogError("箭头未创建！");
            return;
        }
        
        Debug.Log($"=== 箭头指向测试 ===");
        Debug.Log($"智能体位置: {transform.position}");
        Debug.Log($"智能体旋转: {transform.rotation.eulerAngles}");
        Debug.Log($"智能体朝向向量: {transform.forward}");
        Debug.Log($"箭头位置: {arrowIndicator.position}");
        Debug.Log($"箭头旋转: {arrowIndicator.rotation.eulerAngles}");
        Debug.Log($"箭头局部旋转: {arrowIndicator.localRotation.eulerAngles}");
        Debug.Log($"箭头局部位置: {arrowIndicator.localPosition}");
        Debug.Log($"箭头向上向量: {arrowIndicator.up}");
        Debug.Log($"箭头向前向量: {arrowIndicator.forward}");
        Debug.Log($"箭头向右向量: {arrowIndicator.right}");
        
        // 可视化箭头方向
        Debug.DrawRay(arrowIndicator.position, arrowIndicator.forward * 2f, Color.red, 5f);
        Debug.DrawRay(arrowIndicator.position, arrowIndicator.up * 2f, Color.green, 5f);
        Debug.DrawRay(arrowIndicator.position, arrowIndicator.right * 2f, Color.blue, 5f);
        
        Debug.Log($"红色=前向(Z), 绿色=上向(Y), 蓝色=右向(X)");
    }
    
    [ContextMenu("打印可读性策略动作")]
    public void DebugLegiblePolicyAction()
    {
        if (!agentController || !agentController.grid || !agentController.planner)
        {
            Debug.LogError("智能体控制器未初始化！");
            return;
        }
        
        int currentState = agentController.GetCurrentState();
        int action = agentController.planner.LegiblePolicy[currentState];
        
        Debug.Log($"状态 {currentState} 的可读性策略动作: {action} ({GetActionName(action)})");
        Debug.Log($"智能体当前朝向: {transform.forward}");
        
        // 比较当前朝向与动作方向的匹配度
        Vector3[] actionDirs = {
            Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
            Vector3.forward + Vector3.left, Vector3.forward + Vector3.right,
            Vector3.back + Vector3.left, Vector3.back + Vector3.right
        };
        
        if (action < actionDirs.Length)
        {
            Vector3 actionDir = actionDirs[action].normalized;
            float match = Vector3.Dot(transform.forward, actionDir);
            Debug.Log($"朝向与动作方向匹配度: {match:F3} (1=完全匹配)");
        }
    }

    public void SetColorIntensity(float intensity)
    {
        UpdateVisualColor(intensity);
    }
}