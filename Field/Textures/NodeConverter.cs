﻿using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Field.General;
using Field.Models;

namespace Field.Textures;


public class NodeConverter
{
    private string raw_hlsl;
    private StringReader hlsl;
    private StringBuilder bpy;
    private bool bOpacityEnabled = false;
    private List<Texture> textures = new List<Texture>();
    private List<int> samplers = new List<int>();
    private List<Cbuffer> cbuffers = new List<Cbuffer>();
    private List<Input> inputs = new List<Input>();
    private List<Output> outputs = new List<Output>();
    
    public string HlslToBpy(Material material, string hlslText, bool bIsVertexShader)
    {
        this.raw_hlsl = hlslText;
        hlsl = new StringReader(hlslText);
        bpy = new StringBuilder();
        bOpacityEnabled = false;
        ProcessHlslData();
        if (bOpacityEnabled)
        {
            bpy.AppendLine("# masked");
        }
        // WriteTextureComments(material, bIsVertexShader);
        WriteCbuffers(material, bIsVertexShader);
        WriteFunctionDefinition(bIsVertexShader);
        hlsl = new StringReader(hlslText);
        bool success = ConvertInstructions();
        if (!success)
        {
            return "";
        }

        if (!bIsVertexShader)
        {
            AddOutputs();
        }

        WriteFooter(bIsVertexShader);
        return bpy.ToString();
    }

    private void ProcessHlslData()
    {
        string line = string.Empty;
        bool bFindOpacity = false;
        do
        {
            line = hlsl.ReadLine();
            if (line != null)
            {
                if (line.Contains("r0,r1")) // at end of function definition
                {
                    bFindOpacity = true;
                }

                if (bFindOpacity)
                {
                    if (line.Contains("discard"))
                    {
                        bOpacityEnabled = true;
                        break;
                    }
                    continue;
                }

                if (line.Contains("Texture"))
                {
                    Texture texture = new Texture();
                    texture.Dimension = line.Split("<")[0];
                    texture.Type = line.Split("<")[1].Split(">")[0];
                    texture.Variable = line.Split("> ")[1].Split(" :")[0];
                    texture.Index = Int32.TryParse(new string(texture.Variable.Skip(1).ToArray()), out int index) ? index : -1;
                    textures.Add(texture);
                }
                else if (line.Contains("SamplerState"))
                {
                    samplers.Add(line.Split("(")[1].Split(")")[0].Last() - 48);
                }
                else if (line.Contains("cbuffer"))
                {
                    hlsl.ReadLine();
                    line = hlsl.ReadLine();
                    Cbuffer cbuffer = new Cbuffer();
                    cbuffer.Variable = "cb" + line.Split("cb")[1].Split("[")[0];
                    cbuffer.Index = Int32.TryParse(new string(cbuffer.Variable.Skip(2).ToArray()), out int index) ? index : -1;
                    cbuffer.Count = Int32.TryParse(new string(line.Split("[")[1].Split("]")[0]), out int count) ? count : -1;
                    cbuffer.Type = line.Split("cb")[0].Trim();
                    cbuffers.Add(cbuffer);
                }
                else if (line.Contains(" v") && line.Contains(" : ") && !line.Contains("?"))
                {
                    Input input = new Input();
                    input.Variable = "v" + line.Split("v")[1].Split(" : ")[0];
                    input.Index = Int32.TryParse(new string(input.Variable.Skip(1).ToArray()), out int index) ? index : -1;
                    input.Semantic = line.Split(" : ")[1].Split(",")[0];
                    input.Type = line.Split(" v")[0].Trim();
                    inputs.Add(input);
                }
                else if (line.Contains("out") && line.Contains(" : "))
                {
                    Output output = new Output();
                    output.Variable = "o" + line.Split(" o")[2].Split(" : ")[0];
                    output.Index = Int32.TryParse(new string(output.Variable.Skip(1).ToArray()), out int index) ? index : -1;
                    output.Semantic = line.Split(" : ")[1].Split(",")[0];
                    output.Type = line.Split("out ")[1].Split(" o")[0];
                    outputs.Add(output);
                }
            }

        } while (line != null);
    }

    private void WriteCbuffers(Material material, bool bIsVertexShader)
    {
        bpy.AppendLine("### CBUFFERS ###");
        // Try to find matches, pixel shader has Unk2D0 Unk2E0 Unk2F0 Unk300 available
        foreach (var cbuffer in cbuffers)
        {
            bpy.AppendLine($"#static {cbuffer.Type} {cbuffer.Variable}[{cbuffer.Count}]").AppendLine();
            
            dynamic data = null;
            if (bIsVertexShader)
            {
                if (cbuffer.Count == material.Header.Unk90.Count)
                {
                    data = material.Header.Unk90;
                }
                else if (cbuffer.Count == material.Header.UnkA0.Count)
                {
                    data = material.Header.UnkA0;
                }
                else if (cbuffer.Count == material.Header.UnkB0.Count)
                {
                    data = material.Header.UnkB0;
                }
                else if (cbuffer.Count == material.Header.UnkC0.Count)
                {
                    data = material.Header.UnkC0;
                }
            }
            else
            {
                if (cbuffer.Count == material.Header.Unk2D0.Count)
                {
                    data = material.Header.Unk2D0;
                }
                else if (cbuffer.Count == material.Header.Unk2E0.Count)
                {
                    data = material.Header.Unk2E0;
                }
                else if (cbuffer.Count == material.Header.Unk2F0.Count)
                {
                    data = material.Header.Unk2F0;
                }
                else if (cbuffer.Count == material.Header.Unk300.Count)
                {
                    data = material.Header.Unk300;
                }
                else
                {
                    if (material.Header.PSVector4Container.Hash != 0xffff_ffff)
                    {
                        // Try the Vector4 storage file
                        DestinyFile container = new DestinyFile(PackageHandler.GetEntryReference(material.Header.PSVector4Container));
                        byte[] containerData = container.GetData();
                        int num = containerData.Length / 16;
                        if (cbuffer.Count == num)
                        {
                            List<Vector4> float4s = new List<Vector4>();
                            for (int i = 0; i < containerData.Length / 16; i++)
                            {
                                float4s.Add(StructConverter.ToStructure<Vector4>(containerData.Skip(i*16).Take(16).ToArray()));
                            }

                            data = float4s;
                        }                        
                    }

                }
            }


            for (int i = 0; i < cbuffer.Count; i++)
            {
                switch (cbuffer.Type)
                {
                    case "float4":
                        if (data == null) { 
                            bpy.AppendLine($"addFloat4('{cbuffer.Variable}[{i}]', 0.0, 0.0, 0.0, 0.0)"); 
                        }
                        else
                        {
                            try
                            {
                                if (data[i] is Vector4)
                                {
                                    bpy.AppendLine($"addFloat4('{cbuffer.Variable}[{i}]', {data[i].X}, {data[i].Y}, {data[i].Z}, {data[i].W})");
                                }
                                else
                                {
                                    var x = data[i].Unk00.X; // really bad but required
                                    bpy.AppendLine($"addFloat4('{cbuffer.Variable}[{i}]', {x}, {data[i].Unk00.Y}, {data[i].Unk00.Z}, {data[i].Unk00.W})");
                                }
                            }
                            catch (Exception e)  // figure out whats up here, taniks breaks it
                            {
                                bpy.AppendLine($"addFloat4('{cbuffer.Variable}[{i}]', 0.0, 0.0, 0.0, 0.0)");
                            }
                        }
                        break;
                    case "float3":
                        if (data == null) bpy.AppendLine($"addFloat3('{cbuffer.Variable}[{i}]', 0.0, 0.0, 0.0),");
                        else bpy.AppendLine($"addFloat3('{cbuffer.Variable}[{i}]', {data[i].Unk00.X}, {data[i].Unk00.Y}, {data[i].Unk00.Z}),");
                        break;
                    case "float":
                        if (data == null) bpy.AppendLine($"addFloat('{cbuffer.Variable}[{i}]', 0.0)");
                        else bpy.AppendLine($"addFloat4('{cbuffer.Variable}[{i}]', {data[i].Unk00}, 0.0, 0.0, 0.0)");
                        break;
                    default:
                        throw new NotImplementedException();
                }  
            }

            bpy.AppendLine("");
        }
    }
    
    private void WriteFunctionDefinition(bool bIsVertexShader)
    {
        bpy.AppendLine("### Function Definition ###");
        if (!bIsVertexShader)
        {
            bpy.AppendLine("### v[n] vars ###");
            foreach (var i in inputs)
            {
                if (i.Type == "float4")
                {
                    
                    bpy.AppendLine($"addFloat4('{i.Variable}', 1.0, 1.0, 1.0, 1.0)\n");
                }
                else if (i.Type == "float3")
                {
                    bpy.AppendLine($"addFloat3('{i.Variable}', 1.0, 1.0, 1.0)\n");
                }
                else if (i.Type == "uint")
                {
                    bpy.AppendLine($"addFloat('{i.Variable}', 1.0)\n");
                }
            }
        }
        //bpy.AppendLine("#define cmp -").AppendLine("struct shader {");

        //Variable Definitions not needed
        if (bIsVertexShader)
        {
            bpy.AppendLine("### Is Vertex Shader ###");
            //foreach (var output in outputs)
            //{
            //    bpy.AppendLine($"{output.Type} {output.Variable};");
            //}

            //bpy.AppendLine().AppendLine("void main(");
            //foreach (var texture in textures)
            //{
            //    bpy.AppendLine($"   {texture.Type} {texture.Variable},");
            //}
            //for (var i = 0; i < inputs.Count; i++)
            //{
            //    if (i == inputs.Count - 1)
            //    {
            //        bpy.AppendLine($"   {inputs[i].Type} {inputs[i].Variable}) // {inputs[i].Semantic}");
            //    }
            //    else
            //    {
            //        bpy.AppendLine($"   {inputs[i].Type} {inputs[i].Variable}, // {inputs[i].Semantic}");
            //    }
            //}
        }
        else
        {
            bpy.AppendLine("### Is Not Vertex Shader ###");
            //bpy.AppendLine("FMaterialAttributes main(");
            //foreach (var texture in textures)
            //{
            //    bpy.AppendLine($"   {texture.Type} {texture.Variable},");
            //}

            //bpy.AppendLine($"   float2 tx)");

            //bpy.AppendLine("{").AppendLine("    FMaterialAttributes output;");
            // Output render targets, todo support vertex shader
            //bpy.AppendLine("    float4 o0,o1,o2;");
            foreach (var i in inputs)
            {
                if (i.Type == "float4")
                {
                    raw_hlsl = $"{i.Variable}.xyzw = {i.Variable}.xyzw * tx.xyxy;" + raw_hlsl;
                }
                else if (i.Type == "float3")
                {
                    raw_hlsl = $"{i.Variable}.xyz = {i.Variable}.xyz * tx.xyx;" + raw_hlsl;
                }
                else if (i.Type == "uint")
                {
                    raw_hlsl = $"{i.Variable}.x = {i.Variable}.x * tx.x;" + raw_hlsl;
                }
                raw_hlsl.Replace("v0.xyzw = v0.xyzw * tx.xyxy;", "v0.xyzw = v0.xyzw;");
                //bpy.Replace("v0.xyzw = v0.xyzw * tx.xyxy;", "v0.xyzw = v0.xyzw;");
            }
        }
    }

    private bool ConvertInstructions()
    {
        Dictionary<int, Texture> texDict = new Dictionary<int, Texture>();
        foreach (var texture in textures)
        {
            texDict.Add(texture.Index, texture);
        }
        List<int> sortedIndices = texDict.Keys.OrderBy(x => x).ToList();
        string line = hlsl.ReadLine();
        if (line == null)
        {
            // its a broken pixel shader that uses some kind of memory textures
            return false;
        }
        while (!line.Contains("SV_TARGET2"))
        {
            line = hlsl.ReadLine();
            if (line == null)
            {
                // its a broken pixel shader that uses some kind of memory textures
                return false;
            }
        }
        hlsl.ReadLine();
        StringBuilder splitScript = new StringBuilder();
        Regex matchFunction = new Regex("\\).{0,1}[w,x,y,z]{0,4};");
        do
        {
            line = hlsl.ReadLine().Trim();
            if (line != null)
            {
                if (line.Contains("return;"))
                {
                    break;
                }
                //Pre-Process
                if (line.Contains("Sample"))
                {
                    var equal = line.Split("=")[0];
                    var texIndex = Int32.Parse(line.Split(".Sample")[0].Split("t")[1]);
                    var sampleIndex = Int32.Parse(line.Split("(s")[1].Split("_s,")[0]);
                    var sampleUv = line.Split(", ")[1].Split(")")[0];
                    var dotAfter = line.Split(").")[1];
                    // todo add dimension
                    bpy.AppendLine($"{equal}= sample({sortedIndices.IndexOf(texIndex)}, {sampleUv}).{dotAfter}");
                }
                else if (matchFunction.IsMatch(line))
                {
                    //Function of some sort
                    bpy.AppendLine(line + " // FUNCTION");
                }
                //Not a function
                else if (line.Contains("="))
                {
                    bpy.AppendLine(line + " // NOT FUNCTION");
                }
            }
        } while (line != null);

        return true;
    }

    private void AddOutputs()
    {
        string outputString = @"
        ///RT0
        output.BaseColor = o0.xyz; // Albedo
        
        ///RT1

        // Normal
        float3 biased_normal = o1.xyz - float3(0.5, 0.5, 0.5);
        float normal_length = length(biased_normal);
        float3 normal_in_world_space = biased_normal / normal_length;
        normal_in_world_space.z = sqrt(1.0 - saturate(dot(normal_in_world_space.xy, normal_in_world_space.xy)));
        output.Normal = normalize((normal_in_world_space * 2 - 1.5)*0.5 + 0.5);

        // Roughness
        float smoothness = saturate(8 * (normal_length - 0.375));
        output.Roughness = 1 - smoothness;
 
        ///RT2
        output.Metallic = o2.x;
        output.EmissiveColor = (o2.y - 0.5) * 2 * 5 * output.BaseColor;  // the *5 is a scale to make it look good
        output.AmbientOcclusion = o2.y * 2; // Texture AO

        output.OpacityMask = 1;

        return output;
        ";
        bpy.AppendLine(outputString);
    }

    private void WriteFooter(bool bIsVertexShader)
    {
        bpy.AppendLine("}").AppendLine("};");
        if (!bIsVertexShader)
        {
            bpy.AppendLine("shader s;").AppendLine($"return s.main({String.Join(',', textures.Select(x => x.Variable))},tx);");
        }
    }
}