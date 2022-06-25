﻿using System.Collections.Generic;
using Field;
using Field.General;
using Field.Models;

namespace Field.Entities;

public class Entity : Tag
{
    public D2Class_D89A8080 Header;
    public List<EntityResource> Resources;
    
    // Entity features
    public EntitySkeleton Skeleton;
    public EntityModel Model;
    public EntityModel PhysicsModel;
    public EntityControlRig ControlRig;
    
    public Entity(string hash) : base(hash)
    {
    }
    
    public Entity(TagHash hash) : base(hash)
    {
    }

    protected override void ParseStructs()
    {
        Header = ReadHeader<D2Class_D89A8080>();
    }

    protected override void ParseData()
    {
        foreach (var resource in Header.EntityResources)
        {
            switch (resource.ResourceHash.Header.Unk10)
            {
                case D2Class_8A6D8080:  // Entity model
                    Model = ((D2Class_8F6D8080)resource.ResourceHash.Header.Unk18).Model;
                    break;
                case D2Class_5B6D8080:  // Entity physics model
                    PhysicsModel = ((D2Class_6C6D8080)resource.ResourceHash.Header.Unk18).PhysicsModel;
                    break;
                case D2Class_DD818080:  // Entity skeleton
                    Skeleton = new EntitySkeleton(resource.ResourceHash);
                    break;
                case D2Class_668B8080:  // Entity skeleton
                    ControlRig = new EntityControlRig(resource.ResourceHash);
                    break;
                default:
                    // throw new NotImplementedException($"Implement parsing for {resource.ResourceHash.Header.Unk08}");
                    break;
            }
        }
    }

    public List<DynamicPart> Load(ELOD detailLevel)
    {
        var dynamicParts = new List<DynamicPart>();
        if (PhysicsModel != null)
        {
            dynamicParts = dynamicParts.Concat(PhysicsModel.Load(detailLevel)).ToList();
        }
        if (Model != null)
        {
            dynamicParts = dynamicParts.Concat(Model.Load(detailLevel)).ToList();
        }
        return dynamicParts;
    }

    public void SaveMaterialsFromParts(string saveDirectory, List<DynamicPart> dynamicParts)
    {
        Directory.CreateDirectory($"{saveDirectory}/Textures");
        Directory.CreateDirectory($"{saveDirectory}/Shaders");
        foreach (var dynamicPart in dynamicParts)
        {
            dynamicPart.Material.SaveAllTextures($"{saveDirectory}/Textures");
            // dynamicPart.Material.SaveVertexShader(saveDirectory);
            dynamicPart.Material.SavePixelShader($"{saveDirectory}/Shaders");
            // Environment.Exit(5);
        }
    }
}