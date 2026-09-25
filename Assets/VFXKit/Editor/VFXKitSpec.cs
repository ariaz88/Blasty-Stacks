// VFX Kit - the effect spec as Unity's JsonUtility reads it.
// Field names match the JSON exactly (snake_case included). The Python side
// always writes a fully resolved spec, because JsonUtility zero-fills anything
// missing and a zero lifetime or size is an invisible effect with no error.
using System;

namespace VFXKit
{
    [Serializable] public class ColorStop { public float t; public string c; public float a; }
    [Serializable] public class CurveKey { public float t; public float v; }
    [Serializable] public class BurstSpec { public float time; public int count; public int cycles; public float interval; }

    [Serializable]
    public class ShapeSpec
    {
        public string type;
        public float radius;
        public float angle;
        public float arc;
        public float thickness;
        public float[] box;
        public float[] rotation;
        public float[] position;
    }

    [Serializable] public class VelocitySpec { public float x; public float y; public float z; public float radial; public float orbital; }
    [Serializable] public class NoiseSpec { public float strength; public float frequency; public float scroll; }
    [Serializable] public class TrailSpec { public bool enabled; public float lifetime; public CurveKey[] width; public float min_vertex; }
    [Serializable] public class ScrollSpec { public string noise; public float scale; public float[] speed_a; public float[] speed_b; }

    [Serializable]
    public class LayerSpec
    {
        public string name;
        public bool enabled;
        public string texture;
        public int[] tiles;
        public int flipbook_cycles;
        public string shader;
        public float intensity;
        public float softness;
        public ScrollSpec scroll;
        public string render;
        public float length_scale;
        public float velocity_scale;
        public float delay;
        public float[] lifetime;
        public float[] speed;
        public float[] size;
        public float[] rotation;
        public float[] spin;
        public float gravity;
        public int max;
        public string space;
        public float rate;
        public BurstSpec[] bursts;
        public ShapeSpec shape;
        public ColorStop[] color;
        public CurveKey[] size_curve;
        public VelocitySpec velocity;
        public float drag;
        public NoiseSpec noise;
        public TrailSpec trail;
        public int sort;
    }

    [Serializable] public class TextureSpec { public string id; public string file; public string wrap; public int[] tiles; }
    [Serializable] public class ViewSpec { public float[] center; public float size; }

    [Serializable]
    public class EffectSpec
    {
        public string name;
        public bool loop;
        public float duration;
        public bool prewarm;
        public string notes;
        public string reference;
        public ViewSpec view;
        public LayerSpec[] layers;
        public TextureSpec[] textures;
    }
}
