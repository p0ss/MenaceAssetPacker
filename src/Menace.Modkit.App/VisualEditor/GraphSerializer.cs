#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Menace.Modkit.App.VisualEditor;

/// <summary>
/// Serializer for saving and loading TimelineGraph to/from JSON.
/// </summary>
public static class GraphSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Serialize a TimelineGraph to JSON.
    /// </summary>
    public static string Serialize(TimelineGraph graph)
    {
        var dto = new GraphDto
        {
            Name = graph.Name,
            Cycle = graph.Cycle.ToString(),
            Nodes = graph.Nodes.Select(n => new NodeDto
            {
                Id = n.Id,
                Type = n.NodeType.ToString(),
                X = n.X,
                Y = n.Y,
                Title = n.Title,
                Properties = ConvertProperties(n.Properties)
            }).ToList(),
            Connections = graph.Connections.Select(c => new ConnectionDto
            {
                Id = c.Id,
                SourceNodeId = c.SourcePort?.Node?.Id ?? "",
                SourcePort = c.SourcePort?.Name ?? "",
                TargetNodeId = c.TargetPort?.Node?.Id ?? "",
                TargetPort = c.TargetPort?.Name ?? ""
            }).ToList()
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    /// <summary>
    /// Deserialize a TimelineGraph from JSON.
    /// </summary>
    public static TimelineGraph? Deserialize(string json)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<GraphDto>(json, JsonOptions);
            if (dto == null) return null;

            var graph = new TimelineGraph
            {
                Name = dto.Name ?? "Untitled",
                Cycle = Enum.TryParse<GameCycle>(dto.Cycle, out var cycle) ? cycle : GameCycle.Tactical
            };

            // Create nodes using NodeFactory to get proper port configuration
            var nodeMap = new Dictionary<string, TimelineNode>();
            foreach (var nodeDto in dto.Nodes ?? Enumerable.Empty<NodeDto>())
            {
                if (!Enum.TryParse<TimelineNodeType>(nodeDto.Type, out var nodeType))
                    continue;

                var node = NodeFactory.Create(nodeType, nodeDto.X, nodeDto.Y);
                node.Id = nodeDto.Id ?? Guid.NewGuid().ToString();
                node.Title = nodeDto.Title ?? node.Title;

                // Apply properties
                if (nodeDto.Properties != null)
                {
                    foreach (var kvp in nodeDto.Properties)
                    {
                        node.Properties[kvp.Key] = ConvertJsonElement(kvp.Value);
                    }
                }

                graph.Nodes.Add(node);
                nodeMap[node.Id] = node;
            }

            // Create connections
            foreach (var connDto in dto.Connections ?? Enumerable.Empty<ConnectionDto>())
            {
                if (string.IsNullOrEmpty(connDto.SourceNodeId) || string.IsNullOrEmpty(connDto.TargetNodeId))
                    continue;

                if (!nodeMap.TryGetValue(connDto.SourceNodeId, out var sourceNode))
                    continue;
                if (!nodeMap.TryGetValue(connDto.TargetNodeId, out var targetNode))
                    continue;

                var sourcePort = sourceNode.Outputs.FirstOrDefault(p => p.Name == connDto.SourcePort);
                var targetPort = targetNode.Inputs.FirstOrDefault(p => p.Name == connDto.TargetPort);

                if (sourcePort == null || targetPort == null)
                    continue;

                sourcePort.IsConnected = true;
                targetPort.IsConnected = true;

                graph.Connections.Add(new NodeConnection
                {
                    Id = connDto.Id ?? Guid.NewGuid().ToString(),
                    SourcePort = sourcePort,
                    TargetPort = targetPort
                });
            }

            return graph;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Convert properties dictionary to serializable format.
    /// </summary>
    private static Dictionary<string, object?> ConvertProperties(Dictionary<string, object?> properties)
    {
        var result = new Dictionary<string, object?>();
        foreach (var kvp in properties)
        {
            result[kvp.Key] = kvp.Value;
        }
        return result;
    }

    /// <summary>
    /// Convert JsonElement to appropriate .NET type.
    /// </summary>
    private static object? ConvertJsonElement(object? value)
    {
        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number when element.TryGetInt32(out var i) => i,
                JsonValueKind.Number when element.TryGetDouble(out var d) => d,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => element.ToString()
            };
        }
        return value;
    }

    #region DTOs for serialization

    private class GraphDto
    {
        public string? Name { get; set; }
        public string? Cycle { get; set; }
        public List<NodeDto>? Nodes { get; set; }
        public List<ConnectionDto>? Connections { get; set; }
    }

    private class NodeDto
    {
        public string? Id { get; set; }
        public string? Type { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public string? Title { get; set; }
        public Dictionary<string, object?>? Properties { get; set; }
    }

    private class ConnectionDto
    {
        public string? Id { get; set; }
        public string? SourceNodeId { get; set; }
        public string? SourcePort { get; set; }
        public string? TargetNodeId { get; set; }
        public string? TargetPort { get; set; }
    }

    #endregion
}
