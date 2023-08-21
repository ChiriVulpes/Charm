﻿#include "StaticMesh.h"

#include "DDSTextureLoader.h"
#include "Renderer.h"

#include <fstream>
#include <iostream>

struct cb1_InstanceData
{
    XMVECTOR MeshTransform;
    XMVECTOR UVTransform;
    XMVECTOR InstanceTransformMatrices[];
};

struct cb12_View
{
    XMMATRIX View;    // cb12[0,1,2]
    // . . . eyeDirection.x
    // . . . eyeDirection.y
    // . . . eyeDirection.z
    XMVECTOR Unk04;
    XMVECTOR Unk05;
    XMVECTOR NegativeCameraDirection;    // cb12[6]
    XMVECTOR CameraPosition;             // cb12[7]
    XMVECTOR ViewportDimensions;         // cb12[8], 0/1 is width/height, 2/3 is 1/width and 1/height
    XMVECTOR CameraPosition2;            // cb12[10]
};

StaticMesh::StaticMesh(uint32_t hash)
{
}

ID3D11Buffer* StaticMesh::GetIndexBuffer() const
{
    return IndexBuffer;
}

ID3D11Buffer* const* StaticMesh::GetVertexBuffers() const
{
    return VertexBuffers.data();
}

HRESULT StaticMesh::Initialise(ID3D11Device* Device)
{
    this->Device = Device;

    return S_OK;
}

HRESULT StaticMesh::AddStaticMeshBufferGroup(const BufferGroup& bufferGroup)
{
    HRESULT hr = S_OK;

    hr = CreateVertexBuffers(bufferGroup.VertexBuffers);
    if (FAILED(hr))
        return hr;

    hr = CreateIndexBuffer(bufferGroup.IndexBuffer);
    if (FAILED(hr))
        return hr;

    return hr;
}

HRESULT StaticMesh::Render(ID3D11DeviceContext* DeviceContext, Camera* Camera, float DeltaTime)
{
    if (GetVertexBuffers() == nullptr || GetIndexBuffer() == nullptr)
    {
        return E_FAIL;
    }

    UINT strides[2] = {16, 4};
    UINT offsets[2] = {0, 0};
    DeviceContext->IASetVertexBuffers(0, 2, GetVertexBuffers(), strides, offsets);
    DeviceContext->IASetIndexBuffer(GetIndexBuffer(), DXGI_FORMAT_R16_UINT, 0);
    DeviceContext->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);

    // DeviceContext->DrawIndexed(1116, 0, 0);    // 11529 entire thing, 1116 first part lod0
    // DeviceContext->DrawIndexed(3606, 1116, 0);
    // DeviceContext->DrawIndexed(1503, 5718, 0);

    HRESULT hr = S_OK;
    for (auto& part : Parts)
    {
        if (FAILED(hr = part.Render(DeviceContext, Camera, DeltaTime)))
        {
            return hr;
        }
    }

    return S_OK;
}

HRESULT StaticMesh::CreateIndexBuffer(const Blob& indexBlob)
{
    byte* IndexData = new byte[indexBlob.Size];
    memcpy(IndexData, indexBlob.Data, indexBlob.Size);

    D3D11_BUFFER_DESC bd{};
    bd.Usage = D3D11_USAGE_DEFAULT;
    bd.ByteWidth = indexBlob.Size;
    bd.BindFlags = D3D11_BIND_INDEX_BUFFER;
    bd.CPUAccessFlags = 0;

    D3D11_SUBRESOURCE_DATA InitData = {};
    InitData.pSysMem = std::move(IndexData);
    const HRESULT hr = Device->CreateBuffer(&bd, &InitData, &IndexBuffer);
    return hr;
}

HRESULT StaticMesh::CreateVertexBuffers(const Blob vertexBufferBlobs[3])
{
    D3D11_BUFFER_DESC bd{};
    bd.Usage = D3D11_USAGE_DEFAULT;
    bd.BindFlags = D3D11_BIND_VERTEX_BUFFER;
    bd.CPUAccessFlags = 0;

    for (int i = 0; i < 3; i++)
    {
        const Blob& VertexBlob = vertexBufferBlobs[i];

        // copy the memory so we can free the GCHandle in c#
        byte* VertexData = new byte[VertexBlob.Size];
        memcpy(VertexData, VertexBlob.Data, VertexBlob.Size);

        bd.ByteWidth = VertexBlob.Size;

        D3D11_SUBRESOURCE_DATA InitData = {};
        // actually we probably dont need manualy copy as this should copy too? or maybe not idk
        // idk what assignment operator does to a byte array
        InitData.pSysMem = std::move(VertexData);
        ID3D11Buffer* VertexBuffer;
        HRESULT hr = Device->CreateBuffer(&bd, &InitData, &VertexBuffer);
        if (FAILED(hr))
            return hr;
        VertexBuffers.push_back(VertexBuffer);
    }

    return S_OK;
}

HRESULT Part::CreateConstantBuffers(const Blob vsBuffers[16], const Blob psBuffers[16])
{
    std::ifstream ConstantBufferFile1(
        "C:/Users/monta/Desktop/Projects/Charm/Charm/bin/x64/Debug/net7.0-windows/C325BB80/VS_cb1.bin", std::ios::in | std::ios::binary);
    if (!ConstantBufferFile1)
    {
        return MK_E_CANTOPENFILE;
    }
    ConstantBufferFile1.seekg(0, std::ios::end);
    const UINT FileLength1 = ConstantBufferFile1.tellg();
    ConstantBufferFile1.seekg(0, std::ios::beg);
    BYTE* cb1 = new BYTE[FileLength1];
    ConstantBufferFile1.read((char*) cb1, FileLength1);

    D3D11_BUFFER_DESC bd{};
    bd.Usage = D3D11_USAGE_DEFAULT;
    bd.ByteWidth = FileLength1;
    bd.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
    bd.CPUAccessFlags = 0;

    D3D11_SUBRESOURCE_DATA InitData = {};
    InitData.pSysMem = cb1;

    ID3D11Buffer* ConstantBuffer1;
    HRESULT hr = Device->CreateBuffer(&bd, &InitData, &ConstantBuffer1);
    if (FAILED(hr))
        return hr;

    VSConstantBuffers.push_back(new Resource<ID3D11Buffer>(1, ConstantBuffer1));

    std::ifstream ConstantBufferFile12(
        "C:/Users/monta/Desktop/Projects/Charm/Charm/bin/x64/Debug/net7.0-windows/C325BB80/VS_cb12.bin", std::ios::in | std::ios::binary);
    if (!ConstantBufferFile12)
    {
        return MK_E_CANTOPENFILE;
    }
    ConstantBufferFile12.seekg(0, std::ios::end);
    const UINT FileLength12 = ConstantBufferFile12.tellg();
    ConstantBufferFile12.seekg(0, std::ios::beg);
    BYTE* cb12 = new BYTE[FileLength1];
    ConstantBufferFile12.read((char*) cb12, FileLength12);

    bd.Usage = D3D11_USAGE_DEFAULT;
    bd.ByteWidth = FileLength1;
    bd.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
    bd.CPUAccessFlags = 0;

    InitData.pSysMem = cb12;

    ID3D11Buffer* ConstantBuffer12;
    hr = Device->CreateBuffer(&bd, &InitData, &ConstantBuffer12);
    if (FAILED(hr))
        return hr;

    VSConstantBuffers.push_back(new Resource<ID3D11Buffer>(12, ConstantBuffer12));

    // to be able to update the VS, then we copy over to the resource
    bd.BindFlags = 0;
    hr = hr = Device->CreateBuffer(&bd, &InitData, &ViewBuffer);
    if (FAILED(hr))
        return hr;

    std::ifstream ConstantBufferFile0(
        "C:/Users/monta/Desktop/Projects/Charm/Charm/bin/x64/Debug/net7.0-windows/C325BB80/PS_cb0_75FFBA80.bin",
        std::ios::in | std::ios::binary);
    if (!ConstantBufferFile0)
    {
        return MK_E_CANTOPENFILE;
    }
    ConstantBufferFile0.seekg(0, std::ios::end);
    const UINT FileLength0 = ConstantBufferFile0.tellg();
    ConstantBufferFile0.seekg(0, std::ios::beg);
    BYTE* cb0 = new BYTE[FileLength0];
    ConstantBufferFile0.read((char*) cb0, FileLength0);

    bd.Usage = D3D11_USAGE_DEFAULT;
    bd.ByteWidth = FileLength0;
    bd.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
    bd.CPUAccessFlags = 0;

    InitData.pSysMem = cb0;

    ID3D11Buffer* ConstantBuffer;
    hr = Device->CreateBuffer(&bd, &InitData, &ConstantBuffer);
    if (FAILED(hr))
        return hr;

    PSConstantBuffers.push_back(new Resource<ID3D11Buffer>(0, ConstantBuffer));

    std::ifstream PSConstantBufferFile12(
        "C:/Users/monta/Desktop/Projects/Charm/Charm/bin/x64/Debug/net7.0-windows/C325BB80/PS_cb12.bin", std::ios::in | std::ios::binary);
    if (!PSConstantBufferFile12)
    {
        return MK_E_CANTOPENFILE;
    }
    PSConstantBufferFile12.seekg(0, std::ios::end);
    const UINT PSFileLength12 = PSConstantBufferFile12.tellg();
    PSConstantBufferFile12.seekg(0, std::ios::beg);
    BYTE* PScb12 = new BYTE[PSFileLength12];
    PSConstantBufferFile12.read((char*) cb0, PSFileLength12);

    bd.Usage = D3D11_USAGE_DEFAULT;
    bd.ByteWidth = PSFileLength12;
    bd.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
    bd.CPUAccessFlags = 0;

    InitData.pSysMem = PScb12;

    ID3D11Buffer* PSConstantBuffer12;
    hr = Device->CreateBuffer(&bd, &InitData, &PSConstantBuffer12);
    if (FAILED(hr))
        return hr;

    PSConstantBuffers.push_back(new Resource<ID3D11Buffer>(12, PSConstantBuffer12));

    return S_OK;
}

HRESULT Part::CreateTextureResources(const Blob vsTextures[16], const Blob psTextures[16])
{
    const std::vector<LPCWSTR> TextureFiles = {
        L"C:/Users/monta/Desktop/Projects/Charm/Charm/bin/x64/Debug/net7.0-windows/C325BB80/PS_0_AFC5BC80.dds",
        L"C:/Users/monta/Desktop/Projects/Charm/Charm/bin/x64/Debug/net7.0-windows/C325BB80/PS_1_8848A380.dds",
        L"C:/Users/monta/Desktop/Projects/Charm/Charm/bin/x64/Debug/net7.0-windows/C325BB80/PS_2_53C7BC80.dds",
        L"C:/Users/monta/Desktop/Projects/Charm/Charm/bin/x64/Debug/net7.0-windows/C325BB80/PS_3_9742BD80.dds",
        L"C:/Users/monta/Desktop/Projects/Charm/Charm/bin/x64/Debug/net7.0-windows/C325BB80/PS_4_9342BD80.dds",
        L"C:/Users/monta/Desktop/Projects/Charm/Charm/bin/x64/Debug/net7.0-windows/C325BB80/PS_5_7DC5BC80.dds",
        L"C:/Users/monta/Desktop/Projects/Charm/Charm/bin/x64/Debug/net7.0-windows/C325BB80/PS_6_7D63BC80.dds",
        L"C:/Users/monta/Desktop/Projects/Charm/Charm/bin/x64/Debug/net7.0-windows/C325BB80/PS_7_9B42BD80.dds",
        L"C:/Users/monta/Desktop/Projects/Charm/Charm/bin/x64/Debug/net7.0-windows/C325BB80/PS_8_85C5BC80.dds"};

    HRESULT hr = CreateTextureSRVsFromFiles(Device, TextureFiles, TextureSRVs);
    if (FAILED(hr))
        return hr;

    return S_OK;
}

HRESULT Part::CreateSamplers(const Blob vsSamplers[8], const Blob psSamplers[8])
{
    D3D11_SAMPLER_DESC sampDesc = {};
    sampDesc.Filter = D3D11_FILTER_ANISOTROPIC;
    sampDesc.AddressU = D3D11_TEXTURE_ADDRESS_WRAP;
    sampDesc.AddressV = D3D11_TEXTURE_ADDRESS_WRAP;
    sampDesc.AddressW = D3D11_TEXTURE_ADDRESS_WRAP;
    sampDesc.ComparisonFunc = D3D11_COMPARISON_NEVER;
    sampDesc.MaxAnisotropy = 4;
    sampDesc.MinLOD = 0;
    sampDesc.MaxLOD = D3D11_FLOAT32_MAX;
    sampDesc.MipLODBias = 0;
    ID3D11SamplerState* SamplerState;
    HRESULT hr = Device->CreateSamplerState(&sampDesc, &SamplerState);
    if (FAILED(hr))
        return hr;
    SamplerStates.push_back(new Resource<ID3D11SamplerState>(1, SamplerState));

    sampDesc.AddressU = D3D11_TEXTURE_ADDRESS_CLAMP;
    sampDesc.AddressV = D3D11_TEXTURE_ADDRESS_CLAMP;
    sampDesc.AddressW = D3D11_TEXTURE_ADDRESS_CLAMP;
    hr = Device->CreateSamplerState(&sampDesc, &SamplerState);
    if (FAILED(hr))
        return hr;
    SamplerStates.push_back(new Resource<ID3D11SamplerState>(2, SamplerState));

    sampDesc.AddressU = D3D11_TEXTURE_ADDRESS_WRAP;
    sampDesc.AddressV = D3D11_TEXTURE_ADDRESS_WRAP;
    sampDesc.AddressW = D3D11_TEXTURE_ADDRESS_WRAP;
    hr = Device->CreateSamplerState(&sampDesc, &SamplerState);
    if (FAILED(hr))
        return hr;
    SamplerStates.push_back(new Resource<ID3D11SamplerState>(3, SamplerState));

    return S_OK;
}

ID3D11VertexShader* Part::GetVertexShader() const
{
    return VertexShader;
}

ID3D11PixelShader* Part::GetPixelShader() const
{
    return PixelShader;
}

ID3D11InputLayout* Part::GetVertexLayout() const
{
    return VertexLayout;
}

HRESULT Part::Render(ID3D11DeviceContext* DeviceContext, Camera* Camera, float DeltaTime)
{
    if (GetVertexShader() == nullptr || GetPixelShader() == nullptr || GetVertexLayout() == nullptr)
    {
        return E_FAIL;
    }

    DeviceContext->VSSetShader(GetVertexShader(), nullptr, 0);
    DeviceContext->PSSetShader(GetPixelShader(), nullptr, 0);
    DeviceContext->IASetInputLayout(GetVertexLayout());

    cb12_View View;
    View.View = Camera->GetViewMatrix();
    View.View.r[0].m128_f32[3] = Camera->GetDirection().m128_f32[0];
    View.View.r[1].m128_f32[3] = Camera->GetDirection().m128_f32[1];
    View.View.r[2].m128_f32[3] = Camera->GetDirection().m128_f32[2];
    View.View.r[0].m128_f32[2] = 0;
    View.View.r[1].m128_f32[2] = 0;
    View.View.r[2].m128_f32[2] = 0;
    View.CameraPosition = Camera->GetPosition();

    // std::cout << "Camera Position: " << View.CameraPosition.m128_f32[0] << ", " << View.CameraPosition.m128_f32[1] << ", "
    //           << View.CameraPosition.m128_f32[2] << std::endl;
    // std::cout << "Camera Direction: " << Camera->GetDirection().m128_f32[0] << ", " << Camera->GetDirection().m128_f32[1] << ", "
    //           << Camera->GetDirection().m128_f32[2] << std::endl;

    // view matrix
    D3D11_BOX Box;
    Box.left = 0;
    Box.top = 0;
    Box.front = 0;
    Box.right = 0 + 16 * 3;
    Box.bottom = 1;
    Box.back = 1;
    DeviceContext->UpdateSubresource(ViewBuffer, 0, &Box, &View.View, 0, 0);

    // player position
    Box.left = 112;
    Box.top = 0;
    Box.front = 0;
    Box.right = 112 + 16;
    Box.bottom = 1;
    Box.back = 1;
    DeviceContext->UpdateSubresource(ViewBuffer, 0, &Box, &View.CameraPosition, 0, 0);

    for (const auto& ConstantBuffer : VSConstantBuffers)
    {
        if (ConstantBuffer->Slot == 12)
        {
            DeviceContext->CopyResource(ConstantBuffer->ResourcePointer, ViewBuffer);
        }
        DeviceContext->VSSetConstantBuffers(ConstantBuffer->Slot, 1, &ConstantBuffer->ResourcePointer);
    }
    for (const auto& ConstantBuffer : PSConstantBuffers)
    {
        DeviceContext->PSSetConstantBuffers(ConstantBuffer->Slot, 1, &ConstantBuffer->ResourcePointer);
    }

    DeviceContext->DrawIndexed(PartInfo.IndexCount, PartInfo.IndexOffset, 0);
}

HRESULT Part::Initialise(ID3D11Device* device)
{
    Device = device;

    HRESULT hr;
    hr = CreateVertexShader(PartInfo.PartMaterial.VSBytecode);
    if (FAILED(hr))
        return hr;

    hr = CreatePixelShader(PartInfo.PartMaterial.PSBytecode);
    if (FAILED(hr))
        return hr;

    hr = CreateVertexLayout(PartInfo.PartMaterial.InputSignatures, PartInfo.PartMaterial.VSBytecode);
    if (FAILED(hr))
        return hr;

    hr = CreateConstantBuffers(PartInfo.PartMaterial.VSConstantBuffers, PartInfo.PartMaterial.VSConstantBuffers);
    if (FAILED(hr))
        return hr;

    hr = CreateTextureResources(PartInfo.PartMaterial.VSTextures, PartInfo.PartMaterial.PSTextures);
    if (FAILED(hr))
        return hr;

    hr = CreateSamplers(PartInfo.PartMaterial.VSSamplers, PartInfo.PartMaterial.PSSamplers);
    if (FAILED(hr))
        return hr;

    return hr;
}

HRESULT Part::CreateVertexShader(const Blob& shaderBlob)
{
    // does this copy? want to know if its safe or not
    HRESULT hr = Device->CreateVertexShader(shaderBlob.Data, shaderBlob.Size, nullptr, &VertexShader);
    return hr;
}

HRESULT Part::CreatePixelShader(const Blob& shaderBlob)
{
    HRESULT hr = Device->CreatePixelShader(shaderBlob.Data, shaderBlob.Size, nullptr, &PixelShader);
    return hr;
}

static LPCSTR GetSemanticName(InputSemantic semantic)
{
    switch (semantic)
    {
        case Position:
            return "POSITION";
        case Normal:
            return "NORMAL";
        case Tangent:
            return "TANGENT";
        case Colour:
            return "COLOR";
        case Texcoord:
            return "TEXCOORD";
        case BlendIndices:
            return "BLENDINDICES";
        case BlendWeight:
            return "BLENDWEIGHT";
        default:
            return "";
    }
}

HRESULT Part::CreateVertexLayout(const InputSignature inputSignatures[8], const Blob& shaderBlob)
{
    D3D11_INPUT_ELEMENT_DESC layout[8];

    int numElements = 0;

    for (int i = 0; i < 8; i++)
    {
        if (inputSignatures[i].Semantic == InputSemantic::None)
        {
            continue;
        }

        numElements++;
        layout[i] = {};
        layout[i].SemanticName = GetSemanticName(inputSignatures[i].Semantic);
        layout[i].SemanticIndex = inputSignatures[i].SemanticIndex;
        layout[i].Format = (DXGI_FORMAT) inputSignatures[i].DxgiFormat;
        layout[i].InputSlot = inputSignatures[i].BufferIndex;
        layout[i].InputSlotClass = D3D11_INPUT_PER_VERTEX_DATA;
        layout[i].InstanceDataStepRate = 0;
        layout[i].AlignedByteOffset = D3D11_APPEND_ALIGNED_ELEMENT;
    }

    HRESULT hr = Device->CreateInputLayout(layout, numElements, shaderBlob.Data, shaderBlob.Size, &VertexLayout);
    // VertexShaderBlob->Release();

    return hr;
}

HRESULT StaticMesh::AddPart(const PartInfo& partInfo)
{
    Part part(partInfo);
    HRESULT hr = part.Initialise(Device);
    if (FAILED(hr))
    {
        return hr;
    }
    Parts.push_back(std::move(part));

    return hr;
}
