using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Globalization;

/**
界面上有三个输入框，分别对应 X,Y,Z 的值，请实现 {@link Q1.onGenerateBtnClick} 函数，生成一个 10 × 10 的可控随机矩阵，并显示到界面上，矩阵要求如下：
1. {@link COLORS} 中预定义了 5 种颜色
2. 每个点可选 5 种颜色中的 1 种
3. 按照从左到右，从上到下的顺序，依次为每个点生成颜色，(0, 0)为左上⻆点，(9, 9)为右下⻆点，(0, 9)为右上⻆点
4. 点(0, 0)随机在 5 种颜色中选取
5. 其他各点的颜色计算规则如下，设目标点坐标为(m, n）：
    a. (m, n - 1)所属颜色的概率为基准概率加 X%
    b. (m - 1, n)所属颜色的概率为基准概率加 Y%
    c. 如果(m, n - 1)和(m - 1, n)同色，则该颜色的概率为基准概率加 Z%
    d. 其他颜色平分剩下的概率
*/

public class Q1 : MonoBehaviour
{
    private static readonly Color[] COLORS = new Color[]
    {
        Color.red,
        Color.green,
        Color.blue,
        Color.yellow,
        new Color(1f, 0.5f, 0f) // Orange
    };

    // 每个格子的大小
    private const float GRID_ITEM_SIZE = 75f;

    [SerializeField]
    private InputField xInputField = null;

    [SerializeField]
    private InputField yInputField = null;

    [SerializeField]
    private InputField zInputField = null;

    [SerializeField]
    private Transform gridRootNode = null;

    [SerializeField]
    private GameObject gridItemPrefab = null;

    public void OnGenerateBtnClick()
    {
        // TODO: 请在此处开始作答
        if (gridRootNode == null || gridItemPrefab == null)
        {
            return;
        }

        // 解析百分比输入，“10”或“10%”都视为 +0.10
        float x = Mathf.Max(0f, ReadPercent(xInputField));
        float y = Mathf.Max(0f, ReadPercent(yInputField));
        float z = Mathf.Max(0f, ReadPercent(zInputField));

        // 清空旧的格子
        ClearChildren(gridRootNode);

        const int ROWS = 10;
        const int COLS = 10;
        int[,] colorIdx = new int[ROWS, COLS];

        int k = COLORS.Length;         // 5
        float baseP = 1f / k;          // 0.2

        for (int m = 0; m < ROWS; m++)
        {
            for (int n = 0; n < COLS; n++)
            {
                int idx;
                if (m == 0 && n == 0)
                {
                    // (0,0) 等概率随机
                    idx = Random.Range(0, k);
                }
                else
                {
                    int left = (n > 0) ? colorIdx[m, n - 1] : -1; // (m, n-1)
                    int up = (m > 0) ? colorIdx[m - 1, n] : -1; // (m-1, n)

                    float[] probs = BuildProbs(k, baseP, x, y, z, left, up);
                    idx = SampleIndex(probs);
                }

                colorIdx[m, n] = idx;

                // 实例化一个格子并着色
                var go = Instantiate(gridItemPrefab, gridRootNode);
                go.name = $"Cell({m},{n})_Color{idx}";
                var img = go.GetComponent<Image>();
                if (img != null) img.color = COLORS[idx];

                var rt = go.transform as RectTransform;
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    rt.sizeDelta = new Vector2(GRID_ITEM_SIZE, GRID_ITEM_SIZE);
                    rt.anchoredPosition = new Vector2(n * GRID_ITEM_SIZE, -m * GRID_ITEM_SIZE);
                }
            }
        }
    }

    private static float ReadPercent(InputField field, float defaultValue = 0f)
    {
        if (field == null) return defaultValue;
        string t = (field.text ?? "").Trim();
        if (t.EndsWith("%")) t = t.Substring(0, t.Length - 1).Trim();

        if (float.TryParse(t, out float v))
            return v / 100f;

        Debug.LogWarning($"[Q1] 无法解析百分比：\"{field.text}\"，按 {defaultValue * 100f}% 处理。");
        return defaultValue;
    }

    private static float[] BuildProbs(int k, float baseP, float x, float y, float z, int leftIdx, int upIdx)
    {
        var p = new float[k];

        bool hasLeft = leftIdx >= 0;
        bool hasUp = upIdx >= 0;

        if (!hasLeft && !hasUp)
        {
            // 理论只会在 (0,0) 发生，这里给均匀分布
            float u = 1f / k;
            for (int i = 0; i < k; i++) p[i] = u;
            return p;
        }

        //有左无上 x
        if (hasLeft && !hasUp)
        {
            p[leftIdx] = baseP + x;
            FillOthersEven(p, 1f - p[leftIdx], leftIdx);
            return NormalizeClamp(p);
        }
        
        //有上无左 y
        if (!hasLeft && hasUp)
        {
            p[upIdx] = baseP + y;
            FillOthersEven(p, 1f - p[upIdx], upIdx);
            return NormalizeClamp(p);
        }

        // 左上都有
        if (leftIdx == upIdx)
        {
            // 同色：用 Z 覆盖
            p[leftIdx] = baseP + z;
            FillOthersEven(p, 1f - p[leftIdx], leftIdx);
            return NormalizeClamp(p);
        }
        else
        {
            // 异色：左 = 基准 + X，上 = 基准 + Y，其余三色平分剩余
            p[leftIdx] = baseP + x;
            p[upIdx] = baseP + y;
            float remain = 1f - p[leftIdx] - p[upIdx];
            if (remain < 0f) remain = 0f; // 极端输入保护
            int others = k - 2;
            float per = others > 0 ? remain / others : 0f;
            for (int i = 0; i < k; i++)
            {
                if (i != leftIdx && i != upIdx) p[i] = per;
            }
            return NormalizeClamp(p);
        }
    }

    private static void FillOthersEven(float[] p, float remain, int exceptA)
    {
        int k = p.Length;
        int others = k - 1;
        float per = others > 0 ? remain / others : 0f;
        for (int i = 0; i < k; i++)
        {
            if (i == exceptA) continue;
            p[i] = per;
        }
    }

    private static float[] NormalizeClamp(float[] probs)
    {
        float sum = 0f;
        for (int i = 0; i < probs.Length; i++)
        {
            if (float.IsNaN(probs[i]) || probs[i] < 0f) probs[i] = 0f;
            sum += probs[i];
        }

        if (sum <= 1e-6f)
        {
            float u = 1f / probs.Length;
            for (int i = 0; i < probs.Length; i++) probs[i] = u;
            return probs;
        }

        float inv = 1f / sum;
        for (int i = 0; i < probs.Length; i++) probs[i] *= inv;
        return probs;
    }

    private static int SampleIndex(float[] probs)
    {
        float r = Random.value;
        float acc = 0f;
        for (int i = 0; i < probs.Length; i++)
        {
            acc += probs[i];
            if (r <= acc) return i;
        }
        return probs.Length - 1; // 边界处理
    }

    private static void ClearChildren(Transform root)
    {
        if (root == null) return;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
#if UNITY_EDITOR
            if (!Application.isPlaying)
                Object.DestroyImmediate(child.gameObject);
            else
                Object.Destroy(child.gameObject);
#else
            Object.Destroy(child.gameObject);
#endif
        }
    }
}
