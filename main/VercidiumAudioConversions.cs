namespace godot_raytraced_audio;

public partial class VercidiumAudio : Node
{
    public static vaudio.Vector3F ToVAudio(Vector3 v) => new(v.X, v.Y, v.Z);
    public static Vector3 FromVAudio(vaudio.Vector3F v) => new(v.X, v.Y, v.Z);

    public static vaudio.Matrix4F ToVAudio(Transform3D globalTransform)
    {
        var basis = globalTransform.Basis;
        var origin = globalTransform.Origin;

        // Both Godot's Basis and vaudio.Matrix4F are column-major
        return new vaudio.Matrix4F(
            basis.X.X, basis.X.Y, basis.X.Z, 0f,
            basis.Y.X, basis.Y.Y, basis.Y.Z, 0f,
            basis.Z.X, basis.Z.Y, basis.Z.Z, 0f,
            origin.X, origin.Y, origin.Z, 1f
        );
    }

    // Helper method
    static void AddVertexAndUpdateBounds(Vector3 vertex, ref vaudio.Vector3F min, ref vaudio.Vector3F max, List<vaudio.Vector3F> triangles)
    {
        triangles.Add(ToVAudio(vertex));

        // Update bounds
        min.X = Math.Min(min.X, vertex.X);
        min.Y = Math.Min(min.Y, vertex.Y);
        min.Z = Math.Min(min.Z, vertex.Z);
        max.X = Math.Max(max.X, vertex.X);
        max.Y = Math.Max(max.Y, vertex.Y);
        max.Z = Math.Max(max.Z, vertex.Z);
    }

    public static List<vaudio.Vector3F> ConvertMeshToVector3FList(Mesh mesh, out vaudio.Vector3F minOut, out vaudio.Vector3F maxOut)
    {
        List<vaudio.Vector3F> vertices = [];

        var min = vaudio.Vector3F.MAX;
        var max = vaudio.Vector3F.MIN;

        var surfaceCount = mesh.GetSurfaceCount();

        void AddTriangle(Vector3[] normals, int index, Vector3 v0, Vector3 v1, Vector3 v2)
        {
            if (normals != null && index < normals.Length)
            {
                var edge1 = v1 - v0;
                var edge2 = v2 - v0;
                var calculatedNormal = edge1.Cross(edge2).Normalized();
                var meshNormal = normals[index];

                if (calculatedNormal.Dot(meshNormal) < 0)
                {
                    // Flip winding
                    (v1, v2) = (v2, v1);
                }
            }

            vertices.Add(ToVAudio(v0));
            vertices.Add(ToVAudio(v1));
            vertices.Add(ToVAudio(v2));

            // Update bounds
            min.X = Math.Min(min.X, Math.Min(v0.X, Math.Min(v1.X, v2.X)));
            min.Y = Math.Min(min.Y, Math.Min(v0.Y, Math.Min(v1.Y, v2.Y)));
            min.Z = Math.Min(min.Z, Math.Min(v0.Z, Math.Min(v1.Z, v2.Z)));
            max.X = Math.Max(max.X, Math.Max(v0.X, Math.Max(v1.X, v2.X)));
            max.Y = Math.Max(max.Y, Math.Max(v0.Y, Math.Max(v1.Y, v2.Y)));
            max.Z = Math.Max(max.Z, Math.Max(v0.Z, Math.Max(v1.Z, v2.Z)));
        }

        for (var surfaceIndex = 0; surfaceIndex < surfaceCount; surfaceIndex++)
        {
            var arrays = mesh.SurfaceGetArrays(surfaceIndex);
            if (arrays == null || arrays.Count == 0)
                continue;


            // Get vertex array (index 0 in the arrays)
            var surfaceVertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            if (surfaceVertices == null || surfaceVertices.Length == 0)
                continue;


            // Get the normal array to check winding consistency
            Vector3[] normals = null;
            var normalsVariant = arrays[(int)Mesh.ArrayType.Normal];

            if (normalsVariant.VariantType != Variant.Type.Nil)
            {
                // TODO - does AsVector3Array() copy the data? If so is there a faster alternative?
                normals = normalsVariant.AsVector3Array();
            }

            var indicesVariant = arrays[(int)Mesh.ArrayType.Index];

            // No index array - vertices are in triangle order
            if (indicesVariant.VariantType == Variant.Type.Nil)
            {
                for (int i = 0; i < surfaceVertices.Length; i += 3)
                {
                    if (i + 2 >= surfaceVertices.Length)
                        break;

                    var v0 = surfaceVertices[i];
                    var v1 = surfaceVertices[i + 1];
                    var v2 = surfaceVertices[i + 2];

                    AddTriangle(normals, i, v0, v1, v2);
                }
            }
            else
            {
                // Use indices to build triangles
                // TODO - does AsInt32Array() copy the data? If so is there a faster alternative?
                var indices = indicesVariant.AsInt32Array();

                for (int i = 0; i < indices.Length; i += 3)
                {
                    if (i + 2 >= indices.Length)
                        break;

                    var index0 = indices[i];
                    var index1 = indices[i + 1];
                    var index2 = indices[i + 2];

                    var v0 = surfaceVertices[index0];
                    var v1 = surfaceVertices[index1];
                    var v2 = surfaceVertices[index2];

                    AddTriangle(normals, i, v0, v1, v2);
                }
            }
        }

        // If no vertices were found, reset to default bounds
        if (vertices.Count == 0)
        {
            min = vaudio.Vector3F.Zero;
            max = vaudio.Vector3F.Zero;
        }

        minOut = min;
        maxOut = max;

        return vertices;
    }

    public static List<vaudio.Vector3F> ConvertConcavePolygonToVector3FList(ConcavePolygonShape3D shape, out vaudio.Vector3F min, out vaudio.Vector3F max)
    {
        Vector3[] faces = shape.GetFaces();

        if (faces.Length == 0)
        {
            min = new vaudio.Vector3F(0, 0, 0);
            max = new vaudio.Vector3F(0, 0, 0);
            return [];
        }

        List<vaudio.Vector3F> vertices = [];

        min = vaudio.Vector3F.MAX;
        max = vaudio.Vector3F.MIN;

        for (int i = 0; i < faces.Length; i++)
        {
            vaudio.Vector3F vertex = new(
                faces[i].X,
                faces[i].Y,
                faces[i].Z
            );

            vertices.Add(vertex);

            // Update bounds
            min.X = Math.Min(min.X, vertex.X);
            min.Y = Math.Min(min.Y, vertex.Y);
            min.Z = Math.Min(min.Z, vertex.Z);

            max.X = Math.Max(max.X, vertex.X);
            max.Y = Math.Max(max.Y, vertex.Y);
            max.Z = Math.Max(max.Z, vertex.Z);
        }

        return vertices;
    }

    public static List<vaudio.Vector3F> ConvertConvexPolygonToVector3FList(ConvexPolygonShape3D shape, out vaudio.Vector3F min, out vaudio.Vector3F max)
    {
        // Use Godot's built-in debug mesh which properly triangulates the convex hull
        var debugMesh = shape.GetDebugMesh();

        if (debugMesh == null)
        {
            min = new vaudio.Vector3F(0, 0, 0);
            max = new vaudio.Vector3F(0, 0, 0);
            return [];
        }

        return ConvertMeshToVector3FList(debugMesh, out min, out max);
    }

    public static List<vaudio.Vector3F> ConvertHeightMapToVector3FList(HeightMapShape3D shape, out vaudio.Vector3F min, out vaudio.Vector3F max)
    {
        int mapWidth = shape.MapWidth;
        int mapDepth = shape.MapDepth;
        float[] mapData = shape.MapData;

        if (mapWidth < 2 || mapDepth < 2 || mapData.Length < mapWidth * mapDepth)
        {
            min = new vaudio.Vector3F(0, 0, 0);
            max = new vaudio.Vector3F(0, 0, 0);
            return [];
        }

        List<vaudio.Vector3F> triangles = [];

        min = vaudio.Vector3F.MAX;
        max = vaudio.Vector3F.MIN;

        // HeightMapShape3D is centered at origin, spanning from -width/2 to +width/2 and -depth/2 to +depth/2
        float halfWidth = (mapWidth - 1) / 2.0f;
        float halfDepth = (mapDepth - 1) / 2.0f;

        // Generate triangles for each grid cell
        for (int z = 0; z < mapDepth - 1; z++)
        {
            for (int x = 0; x < mapWidth - 1; x++)
            {
                // Get heights for the 4 corners of this cell
                float h00 = mapData[z * mapWidth + x];
                float h10 = mapData[z * mapWidth + (x + 1)];
                float h01 = mapData[(z + 1) * mapWidth + x];
                float h11 = mapData[(z + 1) * mapWidth + (x + 1)];

                // Skip cells with NaN heights (holes in the terrain)
                if (float.IsNaN(h00) || float.IsNaN(h10) || float.IsNaN(h01) || float.IsNaN(h11))
                    continue;

                // Calculate world positions (centered at origin)
                Vector3 v00 = new(x - halfWidth, h00, z - halfDepth);
                Vector3 v10 = new(x + 1 - halfWidth, h10, z - halfDepth);
                Vector3 v01 = new(x - halfWidth, h01, z + 1 - halfDepth);
                Vector3 v11 = new(x + 1 - halfWidth, h11, z + 1 - halfDepth);

                // Update bounds
                foreach (var v in new[] { v00, v10, v01, v11 })
                {
                    min.X = Math.Min(min.X, v.X);
                    min.Y = Math.Min(min.Y, v.Y);
                    min.Z = Math.Min(min.Z, v.Z);
                    max.X = Math.Max(max.X, v.X);
                    max.Y = Math.Max(max.Y, v.Y);
                    max.Z = Math.Max(max.Z, v.Z);
                }

                // Create two triangles for this quad (counter-clockwise winding for upward-facing normals)
                // Triangle 1: v00, v01, v10
                triangles.Add(ToVAudio(v00));
                triangles.Add(ToVAudio(v01));
                triangles.Add(ToVAudio(v10));

                // Triangle 2: v10, v01, v11
                triangles.Add(ToVAudio(v10));
                triangles.Add(ToVAudio(v01));
                triangles.Add(ToVAudio(v11));
            }
        }

        if (triangles.Count == 0)
        {
            min = new vaudio.Vector3F(0, 0, 0);
            max = new vaudio.Vector3F(0, 0, 0);
        }

        return triangles;
    }
}
