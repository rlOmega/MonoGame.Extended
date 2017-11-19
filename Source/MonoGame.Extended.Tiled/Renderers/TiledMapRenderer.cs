﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGame.Extended.Tiled.Renderers
{
    public class TiledMapRenderer : IDisposable
    {
        private readonly TiledMapModel _mapModel;

        private readonly TiledMap _map;
        private readonly TiledMapEffect _defaultEffect;
        private Matrix _worldMatrix = Matrix.Identity;
        private readonly GraphicsDevice _graphicsDevice;

        private readonly Dictionary<TiledMapTileset, List<TiledMapTilesetAnimatedTile>> _animatedTilesByTileset;

        public TiledMapRenderer(GraphicsDevice graphicsDevice, TiledMap map)
        {
            if (graphicsDevice == null) throw new ArgumentNullException(nameof(graphicsDevice));
            if (map == null) throw new ArgumentNullException(nameof(map));

            _map = map;
            _graphicsDevice = graphicsDevice;
            _defaultEffect = new TiledMapEffect(graphicsDevice);

            _animatedTilesByTileset = _map.Tilesets.ToDictionary(i => i, i => i.Tiles.OfType<TiledMapTilesetAnimatedTile>().ToList());

            var modelBuilder = new TiledMapModelBuilder(graphicsDevice, map);
            _mapModel = modelBuilder.Build();
        }

        public void Dispose()
        {
            _defaultEffect.Dispose();
        }

        public void Update(GameTime gameTime)
        {
            for (var tilesetIndex = 0; tilesetIndex < _map.Tilesets.Count; tilesetIndex++)
            {
                var tileset = _map.Tilesets[tilesetIndex];
                var animatedTiles = _animatedTilesByTileset[tileset];

                for (var animatedTileIndex = 0; animatedTileIndex < animatedTiles.Count; animatedTileIndex++)
                {
                    var animatedTilesetTile = animatedTiles[animatedTileIndex];
                    animatedTilesetTile.Update(gameTime);
                }
            }

            UpdateAnimatedLayers(_mapModel.Layers.OfType<TiledMapAnimatedLayerModel>());
        }

        private static unsafe void UpdateAnimatedLayers(IEnumerable<TiledMapAnimatedLayerModel> animatedLayerModels)
        {
            foreach (var animatedModel in animatedLayerModels)
            {
                // update the texture coordinates for each animated tile
                fixed (VertexPositionTexture* fixedVerticesPointer = animatedModel.Vertices)
                {
                    var verticesPointer = fixedVerticesPointer;

                    foreach (var animatedTile in animatedModel.AnimatedTilesetTiles)
                    {
                        var currentFrameTextureCoordinates = animatedTile.CurrentAnimationFrame.TextureCoordinates;

                        (*verticesPointer++).TextureCoordinate = currentFrameTextureCoordinates[0];
                        (*verticesPointer++).TextureCoordinate = currentFrameTextureCoordinates[1];
                        (*verticesPointer++).TextureCoordinate = currentFrameTextureCoordinates[2];
                        (*verticesPointer++).TextureCoordinate = currentFrameTextureCoordinates[3];
                    }
                }

                // copy (upload) the updated vertices to the GPU's memory
                animatedModel.VertexBuffer.SetData(animatedModel.Vertices, 0, animatedModel.Vertices.Length);
            }
        }

        public void Draw(Matrix? viewMatrix = null, Matrix? projectionMatrix = null, Effect effect = null, float depth = 0.0f)
        {
            var viewMatrix1 = viewMatrix ?? Matrix.Identity;
            var projectionMatrix1 = projectionMatrix ?? Matrix.CreateOrthographicOffCenter(0, _graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height, 0, 0, -1);

            Draw(ref viewMatrix1, ref projectionMatrix1, effect, depth);
        }

        public void Draw(ref Matrix viewMatrix, ref Matrix projectionMatrix, Effect effect = null, float depth = 0.0f)
        {
            for (var index = 0; index < _map.Layers.Count; index++)
                Draw(index, ref viewMatrix, ref projectionMatrix, effect, depth);
        }

        public void Draw(int layerIndex, Matrix? viewMatrix = null, Matrix? projectionMatrix = null, Effect effect = null, float depth = 0.0f)
        {
            var viewMatrix1 = viewMatrix ?? Matrix.Identity;
            var projectionMatrix1 = projectionMatrix ?? Matrix.CreateOrthographicOffCenter(0, _graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height, 0, 0, -1);

            Draw(layerIndex, ref viewMatrix1, ref projectionMatrix1, effect, depth);
        }

        public void Draw(int layerIndex, ref Matrix viewMatrix, ref Matrix projectionMatrix, Effect effect = null, float depth = 0.0f)
        {
            var layer = _map.Layers[layerIndex];

            if (!layer.IsVisible)
                return;

            if (layer is TiledMapObjectLayer)
                return;

            _worldMatrix.Translation = new Vector3(layer.Offset.X, layer.Offset.Y, depth);

            var effect1 = effect ?? _defaultEffect;
            var tiledMapEffect = effect1 as ITiledMapEffect;
            if (tiledMapEffect == null)
                return;

            // model-to-world transform
            tiledMapEffect.World = _worldMatrix;
            tiledMapEffect.View = viewMatrix;
            tiledMapEffect.Projection = projectionMatrix;

            foreach (var layerModel in _mapModel.Layers)
            {
                // desired alpha
                tiledMapEffect.Alpha = layer.Opacity;

                // desired texture
                tiledMapEffect.Texture = layerModel.Texture;

                // bind the vertex and index buffer
                _graphicsDevice.SetVertexBuffer(layerModel.VertexBuffer);
                _graphicsDevice.Indices = layerModel.IndexBuffer;

                // for each pass in our effect
                foreach (var pass in effect1.CurrentTechnique.Passes)
                {
                    // apply the pass, effectively choosing which vertex shader and fragment (pixel) shader to use
                    pass.Apply();

                    // draw the geometry from the vertex buffer / index buffer
                    _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, layerModel.TriangleCount);
                }
            }
        }
    }
}