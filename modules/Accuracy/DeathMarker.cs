using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Quartz.Features.Accuracy;
internal static class DeathMarker {
    private const int MaxMarkers = 20;
    private const int Segments = 24;
    private const float Radius = 0.9f;
    private static readonly List<GameObject> markers = [];
    private static Transform root;
    private static Transform Root() {
        if(root != null) return root;
        GameObject obj = new("QuartzAccuracyDeathMarkers");
        Object.DontDestroyOnLoad(obj);
        root = obj.transform;
        return root;
    }
    public static void Mark(List<Vector3> points) {
        if(points == null || points.Count == 0) return;
        foreach(Vector3 point in points) {
            GameObject marker = new("DeathMarker");
            marker.transform.SetParent(Root(), false);
            marker.transform.position = point;
            LineRenderer line = marker.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = Segments;
            line.startWidth = 0.08f;
            line.endWidth = 0.08f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = new Color(1f, 0.2f, 0.2f, 0.75f);
            line.endColor = new Color(1f, 0.2f, 0.2f, 0.75f);
            for(int i = 0; i < Segments; i++) {
                float t = i / (float)Segments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(t) * Radius, Mathf.Sin(t) * Radius, 0f));
            }
            markers.Add(marker);
        }
        while(markers.Count > MaxMarkers) {
            Object.Destroy(markers[0]);
            markers.RemoveAt(0);
        }
    }
    public static void Clear() {
        foreach(GameObject marker in markers)
            if(marker != null) Object.Destroy(marker);
        markers.Clear();
    }
}
