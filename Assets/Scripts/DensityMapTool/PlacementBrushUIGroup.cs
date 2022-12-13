using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.TerrainTools;
using UnityEngine;

public class PlacementBrushUIGroup : BaseBrushUIGroup
{
    public class FeatureDefaults
    {
        public float Size { get; set; }
        public float Rotation { get; set; }
        public float Strength { get; set; }
        public float Spacing { get; set; }
        public float Scatter { get; set; }
    }
    
    [Flags]
    public enum Feature
    {
        Size = 1 << 0,
        Rotation = 1 << 1,
        Strength = 1 << 2,
        Spacing = 1 << 3,
        Scatter = 1 << 4,
        Smoothing = 1 << 5,

        All = Size | Rotation | Strength | Spacing | Scatter | Smoothing,

        NoScatter = All & ~Scatter,
        NoSpacing = All & ~Spacing,
    }
    

    public PlacementBrushUIGroup(string name, Func<TerrainToolsAnalytics.IBrushParameter[]> analyticsCall = null, Feature feature = Feature.All, FeatureDefaults defaults = null) : base(name, analyticsCall)
    {
        terrainUnderCursor = Terrain.activeTerrain; //temp
        
        //Scatter must be first.
        // if ((feature & Feature.Scatter) != 0)
        // {
        //     AddScatterController(new BrushScatterVariator(name, this, this, defaults?.Scatter?? brushScatter));
        // }
        //
        // if ((feature & Feature.Size) != 0)
        // {
        //     AddSizeController(new BrushSizeVariator(name, this, this, defaults?.Size?? brushSize));
        // }
        // if ((feature & Feature.Rotation) != 0)
        // {
        //     AddRotationController(new BrushRotationVariator(name, this, this, false, defaults?.Rotation?? brushRotation));
        // }
        // if ((feature & Feature.Strength) != 0)
        // {
        //     AddStrengthController(new BrushStrengthVariator(name, this, this, defaults?.Strength?? brushStrength));
        // }
        // if ((feature & Feature.Spacing) != 0)
        // {
        //     AddSpacingController(new BrushSpacingVariator(name, this, this, defaults?.Spacing?? brushSpacing));
        // }
        //
        // if ((feature & Feature.Smoothing) != 0)
        // {
        //     AddSmoothingController(new DefaultBrushSmoother(name));
        // }
        //
        // AddModifierKeyController(new DefaultBrushModifierKeys());
    }
}