using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class RedeLabWebGLAuthBuild
{
    private const string OutputDirectory = "Build_WebGL";

    [MenuItem("RedeLab/Build/WebGL Auth Test")]
    public static void Build()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            throw new InvalidOperationException("Nenhuma cena esta habilitada em Build Settings.");
        }

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputDirectory,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        if (summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Build WebGL falhou: {summary.result}, {summary.totalErrors} erro(s).");
        }

        string instructions =
            $"Build WebGL concluido em {OutputDirectory} ({summary.totalSize} bytes).\n" +
            "Nao use o servidor temporario do Unity. Em redelab-server, execute " +
            "'npm run unity-webgl-auth' e abra http://localhost:8081.";
        Debug.Log(instructions);
        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog("RedeLab WebGL pronto", instructions, "OK");
        }
    }
}

public sealed class RedeLabWebGLBuildAndRunGuard : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        bool isUnityBuildAndRun = (report.summary.options & BuildOptions.AutoRunPlayer) != 0;
        if (report.summary.platform != BuildTarget.WebGL || !isUnityBuildAndRun) return;

        throw new BuildFailedException(
            "O Build And Run do Unity usa um SimpleWebServer com porta aleatoria e nao e compativel " +
            "com o callback Auth0 local do RedeLab. Gere pelo menu 'RedeLab > Build > WebGL Auth Test', " +
            "execute 'npm run unity-webgl-auth' em redelab-server e abra http://localhost:8081.");
    }
}
