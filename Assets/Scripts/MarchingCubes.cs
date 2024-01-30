using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.Experimental;
using UnityEngine;
using UnityEngine.Rendering;

public class MarchingCubes2 : MonoBehaviour
{
    struct Triangle
    {
        public Vector4 a, b, c;
    }
    
    [SerializeField] private ComputeShader shader;
    [SerializeField] private VolumetricPass pass;
    [SerializeField] private bool run;
    [SerializeField] private Texture3D density;
    [SerializeField] private int chunkResolution = 2;
    [SerializeField] private int resolution = 4;
    [SerializeField] private int maxTris = 1000;
    [SerializeField] private float minHeight;
    [SerializeField] private float maxHeight;
    [SerializeField] private float width;
    [SerializeField] private Vector3 scale;

    private void OnValidate()
    {
        if (run)
        {
            run = false;

            ComputeBuffer buffer = new ComputeBuffer(maxTris, sizeof(float) * 4 * 3, ComputeBufferType.Append);
            ComputeBuffer count = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);

            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            
            for (int chunkX = 0; chunkX < resolution; chunkX++)
            {
                for (int chunkY = 0; chunkY < resolution; chunkY++)
                {
                    for (int chunkZ = 0; chunkZ < resolution; chunkZ++)
                    {
                        CreateChunk(new Vector3(chunkX, chunkY, chunkZ), buffer, count, verts, tris);
                    }
                }
            }

            // List<Mesh> meshes = new List<Mesh>() {new Mesh() {vertices = verts.ToArray(), triangles = tris.ToArray()}};
            // meshes[0].RecalculateNormals();
            List<Mesh> meshes = SeparateMeshes(verts.ToArray(), tris.ToArray());

            foreach (var mesh in meshes)
            {
                GameObject go = new GameObject();
                go.transform.parent = transform;
                go.transform.localPosition = Vector3.zero;
                MeshFilter filter = go.AddComponent<MeshFilter>();
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                filter.mesh = mesh;
            }

            buffer.Release();
            count.Release();
        }
    }

    private List<Mesh> SeparateMeshes(Vector3[] verticies, int[] triangles)
    {
        List<List<int>> newMeshes = new List<List<int>>();
        List<int> tris = new List<int>(triangles);
        while (tris.Count > 0)
        {
            List<int> newTris = new List<int>();
            HashSet<int> triSet = new HashSet<int>();
            int v0 = tris[0];
            int v1 = tris[1];
            int v2 = tris[2];
            tris.RemoveAt(0);
            tris.RemoveAt(0);
            tris.RemoveAt(0);
            triSet.Add(v0);
            triSet.Add(v1);
            triSet.Add(v2);
            newTris.Add(v0);
            newTris.Add(v1);
            newTris.Add(v2);
            bool found = true;
            while (found)
            {
                found = false;
                for (int i = 0; i < tris.Count; i+=3)
                {
                    if (triSet.Contains(tris[i]) || triSet.Contains(tris[i + 1]) || triSet.Contains(tris[i + 2]))
                    {
                        newTris.Add(tris[i]);
                        newTris.Add(tris[i + 1]);
                        newTris.Add(tris[i + 2]);
                        triSet.Add(tris[i]);
                        triSet.Add(tris[i + 1]);
                        triSet.Add(tris[i + 2]);
                        tris.RemoveAt(i);
                        tris.RemoveAt(i);
                        tris.RemoveAt(i);
                        found = true;
                        break;
                    }
                }
            }

            newMeshes.Add(newTris);
        }

        List<Mesh> meshes = new List<Mesh>();
        for (int i = 0; i < newMeshes.Count; i++)
        {
            int[] newTris = newMeshes[i].ToArray();
            Dictionary<int, int> mapping = new Dictionary<int, int>();
            for (int index = 0; index < newTris.Length; index++)
            {
                if (!mapping.ContainsKey(newTris[index]))
                {
                    mapping.Add(newTris[index], mapping.Count);
                }

                newTris[index] = mapping[newTris[index]];
            }

            Vector3[] verts = new Vector3[mapping.Count];
            foreach (KeyValuePair<int,int> map in mapping)
            {
                verts[map.Value] = verticies[map.Key];
            }
            

            Mesh mesh = new Mesh();
            mesh.indexFormat = IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.triangles = newTris;
            mesh.RecalculateBounds();
            float bound = (mesh.bounds.max - mesh.bounds.min).magnitude / 8000.0f;
            mesh.colors = Enumerable.Repeat(new Color(bound, 0, 0), verts.Length).ToArray();
            
            mesh.Optimize();

            meshes.Add(mesh);
        }

        return meshes;
    }

    private void CreateChunk(Vector3 offset, ComputeBuffer buffer, ComputeBuffer count, List<Vector3> verticies, List<int> triangles)
    {
        buffer.SetCounterValue(0);
        shader.SetTexture(0, "_DensityMap", density);
        shader.SetBuffer(0, "Triangles", buffer);
        shader.SetFloat("_Resolution", chunkResolution * 8 * resolution);
        shader.SetFloat("_Width", width);
        shader.SetFloat("_MinHeight", minHeight);
        shader.SetFloat("_MaxHeight", maxHeight);
        shader.SetVector("_Scale", scale);
        shader.SetVector("_Offset", offset * chunkResolution * 8);
        shader.Dispatch(0, chunkResolution, chunkResolution, chunkResolution);
        ComputeBuffer.CopyCount(buffer, count, 0);
        uint[] value = new uint[1];
        count.GetData(value);
        uint triCount = value[0];
        Debug.Log(triCount);
        Triangle[] tris = new Triangle[triCount];
        buffer.GetData(tris);
        for (int i = 0; i < tris.Length; i++)
        {
            if (CanMerge(verticies, tris[i].a, out int index))
            {
                triangles.Add(index);
            }
            else
            {
                verticies.Add(tris[i].a);
                triangles.Add(verticies.Count - 1);
            }
            if (CanMerge(verticies, tris[i].b, out int index2))
            {
                triangles.Add(index2);
            }
            else
            {
                verticies.Add(tris[i].b);
                triangles.Add(verticies.Count - 1);
            }
            if (CanMerge(verticies, tris[i].c, out int index3))
            {
                triangles.Add(index3);
            }
            else
            {
                verticies.Add(tris[i].c);
                triangles.Add(verticies.Count - 1);
            }
        }
    }

    private bool CanMerge(List<Vector3> verts, Vector3 vert, out int index)
    {
        for (int i = 0; i < verts.Count; i++)
        {
            if ((verts[i] - vert).sqrMagnitude <= 1.0f)
            {
                index = i;
                return true;
            }
        }

        index = -1;
        return false;
    }
}
