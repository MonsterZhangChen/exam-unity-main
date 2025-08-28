using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

/**
**该题目校招岗位可以不作答，社招需要作答。**

按照要求在 {@link Q3.onStartBtnClick} 中编写一段异步任务处理逻辑，具体执行步骤如下：
1. 调用 {@link Q3.loadConfig} 加载配置文件，获取资源列表
2. 根据资源列表调用 {@link Q3.loadFile} 加载资源文件
3. 资源列表中的所有文件加载完毕后，调用 {@link Q3.initSystem} 进行系统初始化
4. 系统初始化完成后，打印日志

附加要求
1. 加载文件时，需要做并发控制，最多并发 3 个文件
2. 加载文件时，需要添加超时控制，超时时间为 3 秒
3. 加载文件失败时，需要对单文件做 backoff retry 处理，重试次数为 3 次
4. 对错误进行捕获并打印输出
*/

public class Q3 : MonoBehaviour
{
    // 并发与超时配置
    private const int MaxConcurrentLoads = 3;
    private static readonly TimeSpan SingleFileTimeout = TimeSpan.FromSeconds(3);

    public async void OnStartBtnClick()
    {
        // TODO: 请在此处开始作答
        try
        {
            // 1) 加载配置，拿到文件列表
            var files = await LoadConfig();

            if (files == null || files.Length == 0)
            {
                Debug.LogWarning("config loaded but file list is empty");
                // 没有需要加载的文件也算完成，直接初始化系统
                await InitSystem();
                Debug.Log("all steps finished");
                return;
            }

            // 2) 根据资源列表加载文件
            var limiter = new SemaphoreSlim(MaxConcurrentLoads, MaxConcurrentLoads);
            var tasks = new List<Task<bool>>(files.Length);

            foreach (var file in files)
            {
                tasks.Add(ProcessSingleFile(file, limiter));
            }

            var results = await Task.WhenAll(tasks);

            // 统计失败数量
            int failedCount = 0;
            for (int i = 0; i < results.Length; i++)
            {
                if (!results[i]) failedCount++;
            }
            Debug.LogError($"file loading finished with {failedCount} failures, abort InitSystem");

            // 3) 初始化系统
            await InitSystem();
        }
        catch (Exception ex)
        {
            Debug.LogError($"unexpected error: {ex}");
        }
    }

    private async Task<bool> ProcessSingleFile(string file, SemaphoreSlim limiter)
    {
        await limiter.WaitAsync();
        try
        {
            int attempt = 0;                   // 0 表示首次尝试
            Exception last = null;
            const int baseBackoffMs = 200;     // 初始回退
            while (attempt <= MaxConcurrentLoads)
            {
                try
                {
                    await WithTimeout(LoadFile(file), SingleFileTimeout);
                }
                catch (Exception ex)
                {
                    last = ex;
                    attempt++;

                    if (attempt > MaxConcurrentLoads)
                    {
                        // 超过重试次数仍失败，抛出最终异常
                        throw last;
                    }

                    // 指数回退
                    float jitter = Random.Range(0.5f, 1.5f);
                    int delayMs = (int)(baseBackoffMs * Math.Pow(2, attempt - 1) * jitter);
                    Debug.LogWarning($"retry {attempt}/{MaxConcurrentLoads} for '{file}' after error: {ex.Message}, backoff {delayMs}ms");
                    await Task.Delay(delayMs);
                }
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"final failure for '{file}': {e.Message}");
            return false;
        }
        finally
        {
            limiter.Release();
        }
    }

    // 通用超时包装（不要求被包装任务可取消；若超时，原任务仍会在后台结束）
    private static async Task WithTimeout(Task task, TimeSpan timeout)
    {
        using (var cts = new CancellationTokenSource())
        {
            var timeoutTask = Task.Delay(timeout, cts.Token);
            var finished = await Task.WhenAny(task, timeoutTask);
            if (finished == task)
            {
                // 任务先完成/抛错，取消timeoutTask并传播结果/异常
                cts.Cancel();
                await task;
            }
            else
            {
                throw new TimeoutException($"operation timed out after {timeout.TotalSeconds:F1}s");
            }
        }
    }

    // #region 以下是辅助测试题而写的一些 mock 函数，请勿修改

    /// <summary>
    /// 加载配置文件
    /// </summary>
    /// <returns>文件列表</returns>
    public async Task<string[]> LoadConfig()
    {
        Debug.Log("load config start");
        await Task.Delay(1000);
        if (Random.value > 0.01f)
        {
            Debug.Log("load config success");
            string[] files = new string[100];
            for (int i = 0; i < 100; i++)
            {
                files[i] = $"file-{i}";
            }
            return files;
        }
        else
        {
            Debug.Log("load config failed");
            throw new System.Exception("Load config failed");
        }
    }

    /// <summary>
    /// 加载文件
    /// </summary>
    /// <param name="file">文件名</param>
    /// <returns></returns>
    public async Task LoadFile(string file)
    {
        Debug.Log($"load file start: {file}");
        await Task.Delay(Random.Range(1000, 5000));
        if (Random.value > 0.01f)
        {
            Debug.Log($"load file success: {file}");
        }
        else
        {
            Debug.Log($"load file failed: {file}");
            throw new System.Exception($"Load file failed: {file}");
        }
    }

    /// <summary>
    /// 初始化系统
    /// </summary>
    /// <returns></returns>
    public async Task InitSystem()
    {
        Debug.Log("init system start");
        await Task.Delay(1000);
        Debug.Log("init system success");
    }

    // #endregion
}
