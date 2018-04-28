﻿using UnityEngine;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using System.Collections;
using System.Collections.Generic;

namespace UMA
{
	public class GeometrySelector : MonoBehaviour
	{
		[HideInInspector]
		public MeshHideAsset meshAsset;

		public BitArray selectedTriangles;

		public bool visualizeNormals = false;
		public float normalsLength = 0.1f;
		public Color32 normalsColor = Color.white;

		public Mesh sharedMesh
		{
			get { return _sharedMesh; }
			set { _sharedMesh = (Mesh)Instantiate(value); Initialize(); }
		}
		private Mesh _sharedMesh;

		//Occlusion mesh options
		public Color32 occlusionColor = Color.white;
		public bool occlusionWireframe = true;

		public Mesh occlusionMesh
		{
			get { return _occlusionMesh; }
			set { _occlusionMesh = value; }
		}
		private Mesh _occlusionMesh;


		public MeshRenderer meshRenderer
		{
			get { return _meshRenderer; }
		}
		private MeshRenderer _meshRenderer;

		public MeshCollider meshCollider
		{
			get { return _meshCollider; }
		}
		private MeshCollider _meshCollider;
		//Use 0 for unselected and 1 for selected
		private Material[] _Materials;

#if UNITY_EDITOR
		public struct SceneInfo
        {
            public string path;
            public string name;
            public OpenSceneMode mode;
        }

        public List<SceneInfo> restoreScenes;
#endif

		public void Initialize()
        {
            gameObject.name = "GeometrySelector";
            if (_sharedMesh == null)
            {
                Debug.LogWarning("GeometrySelector: Initializing with no mesh!");
                return;
            }

            if (meshAsset != null)
            {
                if (meshAsset.asset.meshData.rootBoneHash == UMAUtils.StringToHash("Global"))
                    gameObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            }

            gameObject.transform.hideFlags = HideFlags.NotEditable | HideFlags.HideInInspector;

            if (selectedTriangles == null)
                selectedTriangles = new BitArray(_sharedMesh.triangles.Length / 3);
                
            if( !gameObject.GetComponent<MeshFilter>())
            {
                MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
                meshFilter.mesh = _sharedMesh;
                meshFilter.hideFlags = HideFlags.HideInInspector;
            }

            if( !gameObject.GetComponent<MeshRenderer>())
            {                
                _meshRenderer = gameObject.AddComponent<MeshRenderer>();
                _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _meshRenderer.receiveShadows = false;
                _meshRenderer.hideFlags = HideFlags.HideInInspector;
            }

            if( !gameObject.GetComponent<MeshCollider>())
            {
                _meshCollider = gameObject.AddComponent<MeshCollider>();
                _meshCollider.convex = false;
                _meshCollider.sharedMesh = _sharedMesh;
                _meshCollider.hideFlags = HideFlags.HideInInspector;
            }

            if (_Materials == null)
            {
                _Materials = new Material[2];

                //Selected
                _Materials[1] = new Material(Shader.Find("Standard"));
                _Materials[1].name = "Selected";
                _Materials[1].color = Color.red;

                //UnSelected
                _Materials[0] = new Material(Shader.Find("Standard"));
                _Materials[0].name = "UnSelected";
                _Materials[0].color = Color.gray;

                _sharedMesh.subMeshCount = 2;
                _meshRenderer.sharedMaterials = _Materials;

                _meshRenderer.sharedMaterials[0].hideFlags = HideFlags.HideInInspector;
                _meshRenderer.sharedMaterials[1].hideFlags = HideFlags.HideInInspector;
            }
        }

        public void InitializeFromMeshData(UMAMeshData meshData)
        {
            if (meshData == null)
            {
                Debug.LogError("InitializeFromMeshData: meshData is null!");
                return;
            }

            _sharedMesh = new Mesh();
            _sharedMesh.subMeshCount = meshData.subMeshCount;
            _sharedMesh.vertices = meshData.vertices;
            _sharedMesh.normals = meshData.normals;
            _sharedMesh.tangents = meshData.tangents;
            _sharedMesh.uv = meshData.uv;
            _sharedMesh.uv2 = meshData.uv2;
            _sharedMesh.uv3 = meshData.uv3;
            _sharedMesh.uv4 = meshData.uv4;
            _sharedMesh.colors32 = meshData.colors32;

            for (int i = 0; i < meshData.subMeshCount; i++)
                _sharedMesh.SetTriangles(meshData.submeshes[i].triangles, i);

            Initialize();
        }

        public void SelectAll()
        {
            if (_sharedMesh == null)
                return;
            if (selectedTriangles == null)
                return;

            selectedTriangles.SetAll(true);

            UpdateSelectionMesh();
        }

        public void Invert()
        {
            if (_sharedMesh == null)
                return;

            selectedTriangles = selectedTriangles.Not();

            UpdateSelectionMesh();
        }

        public void ClearAll()
        {
            if (selectedTriangles != null)
            {
                selectedTriangles.SetAll(false);

                UpdateSelectionMesh();
            }
            else
                Debug.LogWarning("selectedTriangles is null! Try starting editing again.");
        }

        public void UpdateSelectionMesh()
        {
            int selectedCount = UMAUtils.GetCardinality(selectedTriangles);
            int[] newSelectedTriangles = new int[selectedCount*3];
            int selectedIndex = 0;

            int[] tris = sharedMesh.triangles;

            for (int i = 0; i < selectedTriangles.Length; i++)
            {                
                if (selectedTriangles[i])
                {
                    newSelectedTriangles[selectedIndex + 0] = tris[(i*3) + 0];
                    newSelectedTriangles[selectedIndex + 1] = tris[(i*3) + 1];
                    newSelectedTriangles[selectedIndex + 2] = tris[(i*3) + 2];
                    selectedIndex += 3;
                }
            }

            sharedMesh.SetTriangles(newSelectedTriangles, 1);
        }

        public void UpdateFromTexture(Texture2D tex)
        {
            if (_sharedMesh == null)
                return;
            
            if (_sharedMesh.uv == null)
            {
                Debug.LogWarning("UpdateFromTexture: This mesh has no uv data!");
                return;
            }

            if (selectedTriangles == null)
            {
                Debug.LogWarning("UpdateFromTexture: selectedTriangles is null!");
                return;
            }

            for (int i = 0; i < meshAsset.asset.meshData.submeshes[0].triangles.Length; i+=3)
            {
                bool selected = false;
                Vector2 centerUV = new Vector2();
                for (int k = 0; k < 3; k++)
                {            
                    int index = meshAsset.asset.meshData.submeshes[0].triangles[i + k];
                    centerUV += meshAsset.asset.meshData.uv[index];
                    int x = Mathf.FloorToInt(meshAsset.asset.meshData.uv[index].x * tex.width);
                    int y = Mathf.FloorToInt(meshAsset.asset.meshData.uv[index].y * tex.height);
                    if (tex.GetPixel(x, y).grayscale > 0.5f)
                        selected = true;
                }

                centerUV = centerUV / 3;
                int centerX = Mathf.FloorToInt(centerUV.x * tex.width);
                int centerY = Mathf.FloorToInt(centerUV.y * tex.height);
                if (tex.GetPixel(centerX, centerY).grayscale > 0.5f)
                    selected = true;

                selectedTriangles[(i/3)] = selected;
            }

            UpdateSelectionMesh();
        }

        {
                return;


            if (_occlusionMesh == null)
                _occlusionMesh = new Mesh();
            else
                _occlusionMesh.Clear();
            
            _occlusionMesh.subMeshCount = meshData.subMeshCount;
            _occlusionMesh.vertices = meshData.vertices;
            _occlusionMesh.normals = meshData.normals;
            _occlusionMesh.tangents = meshData.tangents;
            _occlusionMesh.uv = meshData.uv;
            _occlusionMesh.uv2 = meshData.uv2;
            _occlusionMesh.uv3 = meshData.uv3;
            _occlusionMesh.uv4 = meshData.uv4;
            _occlusionMesh.colors32 = meshData.colors32;

            _occlusionMesh.triangles = new int[0];
			_occlusionMesh.subMeshCount = meshData.subMeshCount;

            for (int i = 0; i < meshData.subMeshCount; i++)
                occlusionMesh.SetTriangles(meshData.submeshes[i].triangles, i);
        }

        {


            //Let's call CreateOcclusionMesh to reset it.

            if (Mathf.Approximately(offset,0) && rot == Vector3.zero && pos == Vector3.zero && s == Vector3.one) //If offset is zero and rot is zero, we can early out because we already reset the mesh.
                 return;

            Quaternion q = Quaternion.Euler(rot);
            Matrix4x4 m = Matrix4x4.TRS(pos, q, s);

            Vector3[] verts = _occlusionMesh.vertices;
            Vector3[] normals = _occlusionMesh.normals;
            Vector3[] newVerts = new Vector3[_occlusionMesh.vertexCount];
            for (int i = 0; i < _occlusionMesh.vertexCount; i++)
            {
                newVerts[i] = verts[i] + (normals[i].normalized * offset);
                newVerts[i] = m.MultiplyPoint3x4(newVerts[i]);
            }
            _occlusionMesh.vertices = newVerts;
        }

        void OnDrawGizmos()
        {            
			if (_occlusionMesh != null)
			{
                Gizmos.color = occlusionColor;
                
                if (occlusionWireframe)
                    Gizmos.DrawWireMesh(_occlusionMesh);
                else
                    Gizmos.DrawMesh(_occlusionMesh);
			}
			if(visualizeNormals)
			{
				Matrix4x4 m = gameObject.transform.localToWorldMatrix;
				Vector3[] targetVerts = sharedMesh.vertices;
				Vector3[] targetNorms = sharedMesh.normals;

				Gizmos.color = normalsColor;

				for (int i = 0; i < targetVerts.Length; i++)
				{
					targetVerts[i] = m.MultiplyPoint3x4(targetVerts[i]);
					targetNorms[i] = m.MultiplyPoint3x4(targetNorms[i]) * normalsLength;
					Gizmos.DrawLine(targetVerts[i], targetVerts[i] + targetNorms[i]);
				}
			}
        }
    }
}
