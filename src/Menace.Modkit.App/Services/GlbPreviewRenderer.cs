using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SharpGLTF.Schema2;
using SkiaSharp;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Simple software renderer for GLB preview thumbnails.
/// Uses SkiaSharp for cross-platform 2D drawing.
/// </summary>
public class GlbPreviewRenderer
{
    /// <summary>
    /// Render a GLB file to a preview bitmap.
    /// </summary>
    public static Bitmap? RenderPreview(string glbPath, int width = 256, int height = 256)
    {
        try
        {
            var model = ModelRoot.Load(glbPath);
            return RenderModel(model, width, height);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error rendering GLB preview: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Render a loaded model to a preview bitmap.
    /// </summary>
    public static Bitmap? RenderModel(ModelRoot model, int width = 256, int height = 256)
    {
        try
        {
            // Extract all vertices and triangles from all meshes
            var (vertices, triangles) = ExtractGeometry(model);

            if (vertices.Count == 0)
                return null;

            // Calculate bounding box and center
            var bounds = CalculateBounds(vertices);
            var center = (bounds.Min + bounds.Max) / 2f;
            var size = bounds.Max - bounds.Min;
            var maxDim = Math.Max(size.X, Math.Max(size.Y, size.Z));

            if (maxDim < 0.0001f)
                return null;

            // Create view and projection matrices
            // Rotate to show model from a 3/4 view angle
            float rotationY = MathF.PI / 6f;  // 30 degrees
            float rotationX = MathF.PI / 8f;  // 22.5 degrees

            var viewMatrix =
                Matrix4x4.CreateTranslation(-center) *
                Matrix4x4.CreateRotationY(rotationY) *
                Matrix4x4.CreateRotationX(rotationX);

            // Orthographic projection scaled to fit
            float scale = 0.8f * Math.Min(width, height) / maxDim;

            // Render to SkiaSharp surface
            using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
            var canvas = surface.Canvas;

            // Dark background
            canvas.Clear(new SKColor(30, 30, 35));

            // Transform and project vertices
            var projectedVerts = new List<(float X, float Y, float Z)>();
            foreach (var v in vertices)
            {
                var transformed = Vector3.Transform(v, viewMatrix);
                float x = width / 2f + transformed.X * scale;
                float y = height / 2f - transformed.Y * scale;  // Flip Y for screen coords
                projectedVerts.Add((x, y, transformed.Z));
            }

            // Sort triangles by depth (painter's algorithm) for simple occlusion
            var sortedTriangles = triangles
                .Select(t => (
                    Triangle: t,
                    Depth: (projectedVerts[t.A].Z + projectedVerts[t.B].Z + projectedVerts[t.C].Z) / 3f
                ))
                .OrderBy(t => t.Depth)  // Draw far triangles first
                .Select(t => t.Triangle)
                .ToList();

            // Draw filled triangles with simple lighting
            using var fillPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            using var edgePaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 0.5f,
                Color = new SKColor(60, 60, 70)
            };

            var lightDir = Vector3.Normalize(new Vector3(0.5f, 1f, 0.8f));

            foreach (var tri in sortedTriangles)
            {
                var v0 = vertices[tri.A];
                var v1 = vertices[tri.B];
                var v2 = vertices[tri.C];

                // Calculate face normal for lighting
                var edge1 = v1 - v0;
                var edge2 = v2 - v0;
                var normal = Vector3.Normalize(Vector3.Cross(edge1, edge2));

                // Transform normal
                var transformedNormal = Vector3.TransformNormal(normal, viewMatrix);
                transformedNormal = Vector3.Normalize(transformedNormal);

                // Simple diffuse lighting
                float diffuse = Math.Max(0, Vector3.Dot(transformedNormal, lightDir));
                float ambient = 0.3f;
                float brightness = ambient + diffuse * 0.7f;

                // Base color (teal-ish to match app theme)
                byte r = (byte)(100 * brightness);
                byte g = (byte)(180 * brightness);
                byte b = (byte)(175 * brightness);

                fillPaint.Color = new SKColor(r, g, b);

                // Get projected coordinates
                var p0 = projectedVerts[tri.A];
                var p1 = projectedVerts[tri.B];
                var p2 = projectedVerts[tri.C];

                // Draw triangle
                using var path = new SKPath();
                path.MoveTo(p0.X, p0.Y);
                path.LineTo(p1.X, p1.Y);
                path.LineTo(p2.X, p2.Y);
                path.Close();

                canvas.DrawPath(path, fillPaint);
                canvas.DrawPath(path, edgePaint);
            }

            // Convert to Avalonia bitmap
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(data.ToArray());

            return new Bitmap(stream);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error rendering model: {ex.Message}");
            return null;
        }
    }

    private static (List<Vector3> Vertices, List<(int A, int B, int C)> Triangles) ExtractGeometry(ModelRoot model)
    {
        var allVertices = new List<Vector3>();
        var allTriangles = new List<(int A, int B, int C)>();

        foreach (var mesh in model.LogicalMeshes)
        {
            foreach (var primitive in mesh.Primitives)
            {
                int vertexOffset = allVertices.Count;

                // Get positions
                var positions = primitive.GetVertexAccessor("POSITION");
                if (positions == null)
                    continue;

                var posArray = positions.AsVector3Array();
                foreach (var pos in posArray)
                {
                    allVertices.Add(pos);
                }

                // Get indices
                var indices = primitive.GetIndexAccessor();
                if (indices != null)
                {
                    var indexArray = indices.AsIndicesArray();
                    for (int i = 0; i + 2 < indexArray.Count; i += 3)
                    {
                        allTriangles.Add((
                            vertexOffset + (int)indexArray[i],
                            vertexOffset + (int)indexArray[i + 1],
                            vertexOffset + (int)indexArray[i + 2]
                        ));
                    }
                }
                else
                {
                    // Non-indexed geometry - treat as triangle list
                    for (int i = 0; i + 2 < posArray.Count; i += 3)
                    {
                        allTriangles.Add((
                            vertexOffset + i,
                            vertexOffset + i + 1,
                            vertexOffset + i + 2
                        ));
                    }
                }
            }
        }

        return (allVertices, allTriangles);
    }

    private static (Vector3 Min, Vector3 Max) CalculateBounds(List<Vector3> vertices)
    {
        if (vertices.Count == 0)
            return (Vector3.Zero, Vector3.Zero);

        var min = vertices[0];
        var max = vertices[0];

        foreach (var v in vertices)
        {
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }

        return (min, max);
    }
}
