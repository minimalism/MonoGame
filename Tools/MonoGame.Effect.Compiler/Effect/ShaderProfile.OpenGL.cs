﻿// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using MonoGame.Effect.TPGParser;

namespace MonoGame.Effect
{
    class OpenGLShaderProfile : ShaderProfile
    {
        private bool _isESSL;
        private bool _useMojo;

        private static readonly Regex GlslPixelShaderRegex = DirectX11ShaderProfile.HlslPixelShaderRegex;
        private static readonly Regex GlslVertexShaderRegex = DirectX11ShaderProfile.HlslVertexShaderRegex;
        private static readonly Regex GlslHullShaderRegex = DirectX11ShaderProfile.HlslHullShaderRegex;
        private static readonly Regex GlslDomainShaderRegex = DirectX11ShaderProfile.HlslDomainShaderRegex;
        private static readonly Regex GlslGeometryShaderRegex = DirectX11ShaderProfile.HlslGeometryShaderRegex;

        public OpenGLShaderProfile()
            : base("OpenGL", 0)
        {
            //
        }

        internal override void AddMacros(Dictionary<string, string> macros, Options options)
        {
            macros.Add("GLSL", "1");
            macros.Add("OPENGL", "1");

            _isESSL = options.IsDefined("ESSL");
            _useMojo = options.IsDefined("MOJO");

            if (!_useMojo)
                macros.Add("SM4", "1");
        }

        internal override Regex GetShaderModelRegex(ShaderStage stage)
        {
            switch (stage)
            {
                case ShaderStage.VertexShader:
                    return GlslVertexShaderRegex;
                case ShaderStage.PixelShader:
                    return GlslPixelShaderRegex;
                default:
                    throw new Exception("GetShaderModelRegex: Unknown shader stage");
            }
        }

        internal override void ValidateShaderModels(PassInfo pass)
        {
            int maxSM = _useMojo ? 3 : 5;
            if (_useMojo)
            {
                int major, minor;
                string extension;

                if (!string.IsNullOrEmpty(pass.vsFunction))
                {
                    ParseShaderModel(pass.vsModel, GlslVertexShaderRegex, out major, out minor, out extension);
                    if (major > maxSM)
                        throw new Exception(String.Format("Invalid profile '{0}'. Vertex shader '{1}' must be SM {2}.0 or lower!", pass.vsModel, pass.vsFunction, maxSM));
                }

                if (!string.IsNullOrEmpty(pass.psFunction))
                {
                    ParseShaderModel(pass.psModel, GlslPixelShaderRegex, out major, out minor, out extension);
                    if (major > maxSM)
                        throw new Exception(String.Format("Invalid profile '{0}'. Pixel shader '{1}' must be SM {2}.0 or lower!", pass.vsModel, pass.psFunction, maxSM));
                }
            }
        }

        internal override ShaderData CreateShader(ShaderResult shaderResult, string shaderFunction, string shaderProfile, ShaderStage shaderStage, EffectObject effect, ref string errorsAndWarnings)
        {
            if (_useMojo)
            {
                // For now GLSL is only supported via translation
                // using MojoShader which works from HLSL bytecode.
                var bytecode = EffectObject.CompileHLSL(shaderResult, shaderFunction, shaderProfile, ref errorsAndWarnings);

                // First look to see if we already created this same shader.
                foreach (var shader in effect.Shaders)
                {
                    if (bytecode.SequenceEqual(shader.Bytecode))
                        return shader;
                }

                var shaderInfo = shaderResult.ShaderInfo;
                var shaderData = ShaderData.CreateGLSL_Mojo(bytecode, shaderStage, effect.ConstantBuffers, effect.Shaders.Count, shaderInfo.SamplerStates, shaderResult.Debug);
                effect.Shaders.Add(shaderData);

                return shaderData;
            }
            else
            {
                ParseShaderModel(shaderProfile, GetShaderModelRegex(shaderStage), out int smMajor, out int smMinor, out string smExtension);

                var shaderInfo = shaderResult.ShaderInfo;
                var sourceCode = shaderResult.FileContent;

                var shaderData = ShaderData.CreateGLSL_Conductor(sourceCode,
                    shaderStage, shaderFunction,
                    smMajor, smMinor, smExtension,
                    effect.Shaders.Count, effect.ConstantBuffers, shaderInfo.SamplerStates, 
                    shaderResult.Debug, _isESSL);

                // See if we already created this same shader.
                foreach (var shader in effect.Shaders)
                {
                    if (shaderData.ShaderCode.SequenceEqual(shader.ShaderCode))
                        return shader;
                }

                effect.Shaders.Add(shaderData);
                return shaderData;
            }
        }
    }
}
