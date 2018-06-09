using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using System.IO;


public class CharacterCombine : MonoBehaviour
{

    public Transform root;
    public Transform boneRoot;

    //1.替换蒙皮网格
    //2.合并所有蒙皮网格

    //3.刷新骨骼

    //4.附加材质

    //5.合并贴图（贴图的宽高最好是2的N次方的值）

    //6.重新计算UV
    // Use this for initialization
    void Start()
    {
        Profiler.BeginSample("测试 Combine Character");

        Combine();
    
        Profiler.EndSample();

    }

    void TestCombineTexture()
    {
    }

    void Combine()
    {
        List<CombineInstance> combineInstances = new List<CombineInstance>();
        List<Texture2D> mainTextures = new List<Texture2D>();
        List<Texture2D> occlusionMaps = new List<Texture2D>();

        List<Vector2[]> uvArray = new List<Vector2[]>();
        List<Transform> boneList = new List<Transform>();

        int uvCnt = 0;
        int combineTexWidth = 0;
        int combineTexHight = 0;

        int combineOccTexWidth = 0;
        int combineOccTexHight = 0;

        Material material = null;

        SkinnedMeshRenderer[] skins = root.GetComponentsInChildren<SkinnedMeshRenderer>();


        Profiler.BeginSample("Combine Character Collect skininfo");

        foreach (var skin in skins)
        {
            if (material == null)
            {
                material = Instantiate(skin.sharedMaterial);
            }

            var mesh = skin.sharedMesh;

            //合并网格
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                CombineInstance instance = new CombineInstance();
                instance.mesh = mesh;
                instance.subMeshIndex = i;
                combineInstances.Add(instance);
            }

            uvArray.Add(mesh.uv);
            uvCnt += mesh.uv.Length;

            //贴图
            Texture tex = skin.material.mainTexture;
            if (tex != null)
            {
                mainTextures.Add(tex as Texture2D);
                combineTexWidth += tex.width;
                combineTexHight += tex.height;
            }

            tex = skin.material.GetTexture("_OcclusionMap");
            if (tex != null)
            {
                occlusionMaps.Add(tex as Texture2D);
                combineOccTexWidth += tex.width;
                combineOccTexHight += tex.height;
            }

            //骨骼
            foreach (Transform bone in skin.bones)
            {
                boneList.Add(bone);
            }

            skin.gameObject.SetActive(false);
        }
        Profiler.EndSample();

        Profiler.BeginSample("Combine Character step");

        SkinnedMeshRenderer smr = root.GetComponent<SkinnedMeshRenderer>();
        if(smr == null)
        {
            smr = root.gameObject.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMaterial = material;
        }

        Profiler.BeginSample("Combine Character step mesh");
        //合并
        Mesh newMesh = new Mesh();
        newMesh.CombineMeshes(combineInstances.ToArray(), true, false);
        smr.sharedMesh = newMesh;
        smr.bones = boneList.ToArray();

        combineTexWidth = near2Pow(combineTexWidth);
        combineTexHight = near2Pow(combineTexHight);

        Profiler.EndSample();

        Profiler.BeginSample("Combine Character pack texture");
        //合并主贴图
        //Texture2D newTex = new Texture2D(combineTexWidth, combineTexHight);
        //Rect[] packRect = newTex.PackTextures(mainTextures.ToArray(),0);
        //smr.material.mainTexture = newTex;

        RenderTexture rt = RenderTexture.GetTemporary(combineTexWidth, combineTexHight);

        Shader sh = Shader.Find("zcd/Combine Texture");
        Material mat = new Material(sh);
        for (int texIdx = 0; texIdx < mainTextures.Count; texIdx++)
        {
            var tex = mainTextures[texIdx];
            Graphics.Blit(tex, rt, mat, texIdx);
        }

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D newTex = new Texture2D(combineTexWidth, combineTexHight);
        newTex.ReadPixels(new Rect(0, 0, combineTexWidth, combineTexHight), 0, 0);
        newTex.Apply();

        smr.material.mainTexture = newTex;
        Rect[] packRect = new Rect[2] { new Rect(0,0,0.5f,0.5f),new Rect(0.5f, 0, 0.5f, 0.5f)};
        RenderTexture.ReleaseTemporary(rt);
        RenderTexture.active = prev;

        //SaveTextureToPNG(newTex, "/Users/zhangfan/Documents/zcd/temp", "combine");





        Profiler.EndSample(); //end pack texture


        ////合并OcclusionMap
        //Profiler.BeginSample("Combine Character step texture pack texture");
        //Texture2D occTex = new Texture2D(combineOccTexWidth, combineOccTexHight);
        //packRect = occTex.PackTextures(occlusionMaps.ToArray(), 0);
        //smr.material.SetTexture("_OcclusionMap", occTex);
        //Profiler.EndSample();


        Profiler.BeginSample("Combine Character step texture computer uv");
        Vector2[] atlasUVs = new Vector2[uvCnt];
        for (int i = 0, j = 0; i < uvArray.Count; i++)
        {
            Rect rect = packRect[i];
            foreach (var uv in uvArray[i])
            {
                atlasUVs[j].x = Mathf.Lerp(rect.xMin, rect.xMax, uv.x);
                atlasUVs[j].y = Mathf.Lerp(rect.yMin, rect.yMax, uv.y);
                j++;
            }
        }
        smr.sharedMesh.uv = atlasUVs;

        Profiler.EndSample(); //end computer uv


        Profiler.EndSample();
    }

    int near2Pow(int into)
    {
        int ret = 1;
        for (int i = 0; i < 10; i++)
        {
            if((ret*=2) > into)
            {
                break;
            }
        }

        return ret;
    }

    //将RenderTexture保存成一张png图片  
    public bool SaveRenderTextureToPNG(RenderTexture rt, string contents, string pngName)
    {
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D png = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false);
        png.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);

        SaveTextureToPNG(png, contents, pngName);

        Texture2D.DestroyImmediate(png);  
        png = null;
        RenderTexture.active = prev;
        return true;
    }

    public void SaveTextureToPNG(Texture2D tex, string contents, string pngName)
    {
        if (!Directory.Exists(contents))
            Directory.CreateDirectory(contents);
        FileStream file = File.Open(contents + "/" + pngName + ".png", FileMode.Create);

        BinaryWriter writer = new BinaryWriter(file);
        writer.Write(tex.EncodeToPNG());
        file.Close();
    }
}
