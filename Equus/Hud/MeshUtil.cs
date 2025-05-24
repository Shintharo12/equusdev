using System;
using Vintagestory.API.Client;

namespace Equus.Hud
{
    public static class MeshUtil
    {
        public static MeshData GetRing(float innerRadius, float outerRadius, int divisions, double angleRange, int color)
        {
            divisions *= 4;
            MeshData mesh = new(divisions * 2 * 2, divisions * 2 * 3, false, true, true, false);
            mesh.SetMode(EnumDrawMode.Triangles);           

            for (int i = 0; i <= divisions; i++)
            {
                double angle = i / (double)divisions * angleRange;
                mesh.AddVertex((float)Math.Cos(angle) * innerRadius, (float)Math.Sin(angle) * innerRadius, 0f, 0f, color);
                mesh.AddVertex((float)Math.Cos(angle) * outerRadius, (float)Math.Sin(angle) * outerRadius, 0f, 0f, color);
            }

            for (int j = 0; j < divisions; j++)
            {
                int[] indices = mesh.Indices;
                MeshData meshData = mesh;
                int indicesCount = meshData.IndicesCount;
                meshData.IndicesCount = indicesCount + 1;
                indices[indicesCount] = (short)(j * 2);
                int[] indices2 = mesh.Indices;
                MeshData meshData2 = mesh;
                indicesCount = meshData2.IndicesCount;
                meshData2.IndicesCount = indicesCount + 1;
                indices2[indicesCount] = (short)(j * 2 + 1);
                int[] indices3 = mesh.Indices;
                MeshData meshData3 = mesh;
                indicesCount = meshData3.IndicesCount;
                meshData3.IndicesCount = indicesCount + 1;
                indices3[indicesCount] = (short)((j * 2 + 2) % (divisions * 2 + 2));
                int[] indices4 = mesh.Indices;
                MeshData meshData4 = mesh;
                indicesCount = meshData4.IndicesCount;
                meshData4.IndicesCount = indicesCount + 1;
                indices4[indicesCount] = (short)(j * 2 + 1);
                int[] indices5 = mesh.Indices;
                MeshData meshData5 = mesh;
                indicesCount = meshData5.IndicesCount;
                meshData5.IndicesCount = indicesCount + 1;
                indices5[indicesCount] = (short)((j * 2 + 3) % (divisions * 2 + 2));
                int[] indices6 = mesh.Indices;
                MeshData meshData6 = mesh;
                indicesCount = meshData6.IndicesCount;
                meshData6.IndicesCount = indicesCount + 1;
                indices6[indicesCount] = (short)((j * 2 + 2) % (divisions * 2 + 2));
            }

            return mesh;
        }
    }
}
