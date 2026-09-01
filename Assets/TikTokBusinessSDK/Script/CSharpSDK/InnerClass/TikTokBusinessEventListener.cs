using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using SDK;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TikTokBusinessEventListener : MonoBehaviour
{
    //缓存一下反射的FieldInfo，可以提高性能
    private float _startTime;
    private float _endTime;
    private Dictionary<string, object> _clickInfos;
    // 熔断标记：如果检测到老版 Input 被禁用，设为 true，后续帧直接跳过，不再报错
    private bool _isLegacyInputBlocked = false;
    private void Start()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }
    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }
    private void Update()
    {
        // 1. 业务开关拦截
        if (!TikTokInnerManager.Instance().ShouldReportClickEvent()) return;

        // 2. 核心保护拦截：如果 API 已确定不可用，静默退出，绝不报错
        if (_isLegacyInputBlocked) return;

        try
        {
            // ==================== 触发检测层 ====================
            bool isPointerUp = false;
            bool isPointerDown = false;
            Vector2 inputPosition = Vector2.zero;

            // 优先检测移动端多点触控 (Touch)
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) isPointerUp = true;
                if (touch.phase == TouchPhase.Began) isPointerDown = true;
                inputPosition = touch.position;
            }
            // 兜底检测 PC 端鼠标
            else 
            {
                if (Input.GetMouseButtonUp(0)) isPointerUp = true;
                if (Input.GetMouseButtonDown(0)) isPointerDown = true;
                inputPosition = Input.mousePosition;
            }

            // ==================== 数据处理层 ====================

            // 1. 抬起时上报逻辑
            if (isPointerUp && _clickInfos != null)
            {
                _endTime = Time.unscaledTime;
                var clickDuration = (long)((_endTime - _startTime) * 1000);
                _clickInfos["click_duration"] = clickDuration;
                _clickInfos["platform"] = "unity";
                _clickInfos["monitor_type"] = "enhanced_data_postback";
                
                TikTokBusinessSDK.TrackTTEvent(new TikTokBaseEvent("click", _clickInfos, ""));
                TikTokLogger.Verbose("Unity edp click tracked successfully.");
                
                _clickInfos = null;
            }

            // 2. 按下时抓取逻辑
            if (isPointerDown)
            {
                _clickInfos = null; 
                var eventSystem = EventSystem.current;
                if (eventSystem == null) return;

                // 构造射线
                PointerEventData pointerEventData = new PointerEventData(eventSystem);
                pointerEventData.position = inputPosition;

                List<RaycastResult> results = new List<RaycastResult>();
                eventSystem.RaycastAll(pointerEventData, results);

                if (results.Count == 0) return; 

                GameObject targetGo = results[0].gameObject;

                if (TikTokInnerManager.Instance().IsButtonInBlackList(targetGo.name)) return;

                if (targetGo.GetComponent<Text>() != null || targetGo.GetComponent<Image>() != null)
                {
                    if (targetGo.transform.parent != null) targetGo = targetGo.transform.parent.gameObject;
                }

                _startTime = Time.unscaledTime;

                Dictionary<string, object> info = new Dictionary<string, object>();
                info.Add("class_name", targetGo.name);
                info.Add("click_position_x", inputPosition.x);
                info.Add("click_position_y", Screen.height - inputPosition.y); 

                // 调用之前的局部树生成算法
                int maxDepth = (int)TikTokInnerManager.Instance().PageDeepCount();
                Dictionary<string, object> localTreeRoot = BuildLocalUITreeBottomUp(targetGo.transform, maxDepth);

                info.Add("page_components", localTreeRoot);
                
                Dictionary<string, object> clickComponentTree = BuildClickComponentTree(targetGo.transform, 0, maxDepth);
                info.Add("click_component", clickComponentTree);
                
                string rootName = localTreeRoot.ContainsKey("class_name") ? localTreeRoot["class_name"].ToString() : "Unknown";
                info.Add("current_page_name", rootName);
                info.Add("page_deep_count", maxDepth);

                _clickInfos = info;
            }
        }
        catch (System.InvalidOperationException)
        {
            // 【核心保护】捕获到了新版 Input System 导致的异常
            _isLegacyInputBlocked = true; // 触发熔断
            TikTokLogger.Verbose("Legacy Input Manager is disabled by the project. Click tracker is safely paused to prevent errors.");
        }
        catch (System.Exception e)
        {
            // 其他未知报错防崩溃兜底
            TikTokLogger.Verbose("TikTok click tracker encountered an error: " + e.Message);
        }
    }
    


    private Dictionary<string, object> BuildLocalUITreeBottomUp(Transform targetTransform, int maxDepth)
    {
        Transform current = targetTransform;
        Dictionary<string, object> previousLevelMainNode = null;
        int currentDepth = 0;
        int maxSiblings = maxDepth; // 将深度同步用作宽度限制，保证最大 O(N^2) 的节点数

        // 向上循环爬升
        while (current != null && currentDepth < maxDepth)
        {
            Transform parent = current.parent;

            // 1. 提取当前“主干节点”的属性
            bool isTarget = (current == targetTransform);
            Dictionary<string, object> mainNodeInfo = ExtractNodeProperties(current, isTarget);

            // 2. 将下层传上来的主干拼接为 child_views（藤蔓往上长）
            List<object> childViews = new List<object>();
            if (previousLevelMainNode != null)
            {
                childViews.Add(previousLevelMainNode);
            }

            // 3. 抓取滑动窗口内的兄弟节点（光秃秃的旁支，不再往下遍历）
            if (parent != null)
            {
                int siblingCount = parent.childCount;
                int myIndex = current.GetSiblingIndex();
                
                // 计算滑动窗口的起始和结束索引，保证自己尽量在中心
                int halfWindow = maxSiblings / 2;
                int startIndex = Mathf.Max(0, myIndex - halfWindow);
                int endIndex = startIndex + maxSiblings - 1;
                
                // 窗口触碰右边界，向左平移补足
                if (endIndex >= siblingCount)
                {
                    endIndex = siblingCount - 1;
                    startIndex = Mathf.Max(0, endIndex - maxSiblings + 1);
                }

                // 遍历截取出的窗口
                for (int i = startIndex; i <= endIndex; i++)
                {
                    if (i == myIndex) continue; // 排除自己（已经作为主干处理了）
                    
                    Transform sibling = parent.GetChild(i);
                    if (sibling.gameObject.activeSelf)
                    {
                        Dictionary<string, object> siblingInfo = ExtractNodeProperties(sibling, false);
                        childViews.Add(siblingInfo); // 作为旁支加入当前层
                    }
                }
            }

            if (childViews.Count > 0)
            {
                mainNodeInfo["child_views"] = childViews;
            }

            // 完成本层组装，交棒给上一层
            previousLevelMainNode = mainNodeInfo;
            current = parent;
            currentDepth++;
        }

        // 循环结束时，previousLevelMainNode 就是这棵修剪过的局部树的最高顶点
        return previousLevelMainNode;
    }
    
    private Dictionary<string, object> ExtractNodeProperties(Transform nodeTransform, bool isClickTarget)
    {
        Dictionary<string, object> info = new Dictionary<string, object>();
        info.Add("class_name", nodeTransform.name);
        info.Add("is_click", isClickTarget ? 1 : 0);

        // 提取精确屏幕坐标（无论 Canvas 何种模式都绝对准确）
        RectTransform rectTransform = nodeTransform.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            // corners[1] 是世界坐标下的左上角
            Vector2 screenTopLeft = RectTransformUtility.WorldToScreenPoint(null, corners[1]);

            info.Add("left", screenTopLeft.x);
            info.Add("top", Screen.height - screenTopLeft.y); // 转为 Web 左上角坐标系
            info.Add("width", rectTransform.rect.width);
            info.Add("height", rectTransform.rect.height);
        }

        // 提取组件内容
        Text textComponent = nodeTransform.GetComponent<Text>();
        if (textComponent != null)
        {
            string textString = TikTokInnerManager.Instance().SensigFiltering(textComponent.text);
            info.Add("text", textString);
        }

        Image imageComponent = nodeTransform.GetComponent<Image>();
        if (imageComponent != null && imageComponent.sprite != null && imageComponent.sprite.texture != null)
        {
            info.Add("image_name", imageComponent.sprite.texture.name);
        }

        return info;
    }
    
    private Dictionary<string, object> BuildClickComponentTree(Transform currentTransform, int currentDepth, int maxDepth)
    {
        // 1. 提取当前节点属性（只有第 0 层的才是真正的点击锚点）
        bool isClickTarget = (currentDepth == 0);
        Dictionary<string, object> info = ExtractNodeProperties(currentTransform, isClickTarget);

        // 2. 向下递归获取内部子节点（加入深度限制防死循环或误点超大面板）
        if (currentDepth < maxDepth)
        {
            List<object> childViews = new List<object>();
            for (int i = 0; i < currentTransform.childCount; i++)
            {
                Transform child = currentTransform.GetChild(i);
                if (child.gameObject.activeSelf) // 只抓取激活状态的内部节点
                {
                    childViews.Add(BuildClickComponentTree(child, currentDepth + 1, maxDepth));
                }
            }

            // 如果有内部子节点，挂载到 child_views 上
            if (childViews.Count > 0)
            {
                info["child_views"] = childViews;
            }
        }

        return info;
    }
    
    private void OnActiveSceneChanged(Scene previousScene, Scene newScene)
    {
        if (!TikTokInnerManager.Instance().IsUnityEDPSceneTrackEnable()) return;
        if (!newScene.IsValid()) return;
        string currentScenePath = string.IsNullOrEmpty(newScene.path) ? newScene.name : newScene.path;
        Dictionary<string, object> sceneDictionary = new Dictionary<string, object>();
        Dictionary<string, object> sceneInfo = new Dictionary<string, object>();
        sceneInfo["name"] = currentScenePath;
        sceneDictionary["current_scene"] = sceneInfo;
        
        sceneDictionary.Add("platform","unity");
        sceneDictionary.Add("monitor_type","enhanced_data_postback");
        TikTokBusinessSDK.TrackTTEvent(new TikTokBaseEvent("scene",sceneDictionary,""));
        TikTokLogger.Verbose("Unity edp scene");
    }
}