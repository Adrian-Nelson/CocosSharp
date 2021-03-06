using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework.Graphics;

namespace CocosSharp
{
    public class CCTMXLayer : CCSpriteBatchNode
    {
        // ivars

        bool useAutomaticVertexZ;
        int vertexZvalue;

        uint maxGID;
        uint minGID;

        byte opacity;

        List<int> atlasIndexArray;
        CCSprite reusedTile;


        #region Properties

        // Static properties

        public static float DefaultTexelToContentSizeRatio
        {
            set { DefaultTexelToContentSizeRatios = new CCSize(value, value); }
        }

        public static CCSize DefaultTexelToContentSizeRatios { get; set; }


        // Instance properties

        public string LayerName { get; set; }

        public CCTMXOrientation LayerOrientation { get; set; }          // Should be same as map orientation
        public CCSize LayerSize { get; set; }
        public CCSize MapTileSize { get; set; }                         // Size of the map's tile (could be differnt from the tile's size)

        public uint[] Tiles { get; set; }
        public CCTMXTilesetInfo TileSet { get; set; }

        public Dictionary<string, string> Properties { get; set; }      // Properties of the tmx layer

        public override CCScene Scene 
        { 
            get { return base.Scene; }
            internal set 
            {
                base.Scene = value;

                if(value != null && reusedTile != null)
                {
                    reusedTile.Scene = value;
                }
            }
        }

        public override CCLayer Layer 
        { 
            get { return base.Layer; }
            internal set 
            {
                base.Layer = value;

                if(value != null && reusedTile != null)
                {
                    reusedTile.Layer = value;
                }
            }
        }

        #endregion Properties


        #region Constructors

        static CCTMXLayer()
        {
            DefaultTexelToContentSizeRatios = CCSize.One;
        }

        public CCTMXLayer(CCTMXTilesetInfo tileSetInfo, CCTMXLayerInfo layerInfo, CCTMXMapInfo mapInfo)
            : this(tileSetInfo, layerInfo, mapInfo, layerInfo.LayerSize)
        {
        }

        // Private constructor chaining

        CCTMXLayer(CCTMXTilesetInfo tileSetInfo, CCTMXLayerInfo layerInfo, CCTMXMapInfo mapInfo, CCSize layerSize) 
            : this(tileSetInfo, layerInfo, mapInfo, layerSize, (int)(layerSize.Width * layerSize.Height))
        {
        }

        CCTMXLayer(CCTMXTilesetInfo tileSetInfo, CCTMXLayerInfo layerInfo, CCTMXMapInfo mapInfo, CCSize layerSize, int totalNumberOfTiles) 
            : this(tileSetInfo, layerInfo, mapInfo, layerSize, totalNumberOfTiles, (int)(totalNumberOfTiles * 0.35f + 1), 
                CCTextureCache.SharedTextureCache.AddImage(tileSetInfo.SourceImage))
        {
        }


        CCTMXLayer(CCTMXTilesetInfo tileSetInfo, CCTMXLayerInfo layerInfo, CCTMXMapInfo mapInfo, CCSize layerSize, 
            int totalNumberOfTiles, int tileCapacity, CCTexture2D texture)
            : base(texture, tileCapacity)
        {
            // layerInfo
            LayerName = layerInfo.Name;
            LayerSize = layerSize;
            Tiles = layerInfo.Tiles;
            minGID = layerInfo.MinGID;
            maxGID = layerInfo.MaxGID;
            opacity = layerInfo.Opacity;
            Properties = new Dictionary<string, string>(layerInfo.Properties);

            // TileSetInfo
            TileSet = tileSetInfo;

            // mapInfo
            MapTileSize = mapInfo.TileSize;
            LayerOrientation = (CCTMXOrientation) mapInfo.Orientation;

            // offset (after layer orientation is set);
            CCPoint offset = ApplyLayerOffset(layerInfo.Offset);
            Position = offset;

            atlasIndexArray = new List<int>((int) totalNumberOfTiles);

            ContentSize = LayerSize * MapTileSize;

            useAutomaticVertexZ = false;
            vertexZvalue = 0;
        }

        #endregion Constructors


        public virtual void ReleaseMap()
        {
            Tiles = null;
            atlasIndexArray = null;
        }

        public virtual String PropertyNamed(string propertyName)
        {
            if (Properties.ContainsKey(propertyName))
            {
                return Properties[propertyName];
            }
            else
            {
                return String.Empty;
            }
        }

        void ParseInternalProperties()
        {
            string vertexz = PropertyNamed("cc_vertexz");
            if (!String.IsNullOrEmpty(vertexz))
            {
                if (vertexz == "automatic")
                {
                    useAutomaticVertexZ = true;
                }
                else
                {
                    vertexZvalue = CCUtils.CCParseInt(vertexz);
                }
            }
        }

        int AtlasIndexForExistantZ(int z)
        {
            int index = atlasIndexArray.BinarySearch(z);

            Debug.Assert(index != -1, "TMX atlas index not found. Shall not happen");

            return index;
        }

        int AtlasIndexForNewZ(int z)
        {
            // XXX: This can be improved with a sort of binary search
            int i, count;
            for (i = 0, count = atlasIndexArray.Count; i < count; i++)
            {
                if (z < atlasIndexArray[i])
                {
                    break;
                }
            }

            return i;
        }

        public override void Visit()
        {
            // CAREFUL:
            // This visit is almost identical to CCNode#visit
            // with the exception that it doesn't call visit on it's children
            //
            // The alternative is to have a void CCSprite#visit, but
            // although this is less mantainable, is faster
            //
            if (!Visible || Window == null)
            {
                return;
            }

            Window.DrawManager.PushMatrix();

            if (Grid != null && Grid.Active)
            {
                Grid.BeforeDraw();
                TransformAncestors();
            }

            SortAllChildren();
            Transform();

            Draw();

            if (Grid != null && Grid.Active)
            {
                Grid.AfterDraw(this);
            }

            Window.DrawManager.PopMatrix();
        }

        protected override void Draw()
        {
            var alphaTest = Window.DrawManager.AlphaTestEffect;

            alphaTest.AlphaFunction = CompareFunction.Greater;
            alphaTest.ReferenceAlpha = 0;

            Window.DrawManager.PushEffect(alphaTest);

            base.Draw();

            Window.DrawManager.PopEffect();
        }


        #region Child managment

        public override void AddChild(CCNode child, int zOrder, int tag)
        {
            Debug.Assert(false, "addChild: is not supported on CCTMXLayer. Instead use setTileGID:at:/tileAt:");
        }

        public override void RemoveChild(CCNode node, bool cleanup)
        {
            var sprite = (CCSprite) node;

            if (sprite == null)
            {
                return;
            }

            Debug.Assert(Children.Contains(sprite), "Tile does not belong to TMXLayer");

            int atlasIndex = sprite.AtlasIndex;
            int zz = atlasIndexArray[atlasIndex];
            Tiles[zz] = 0;
            atlasIndexArray.RemoveAt(atlasIndex);
            base.RemoveChild(sprite, cleanup);
        }

        #endregion Child management


        #region Tile managment

        internal virtual void SetupTiles()
        {
            // Optimization: quick hack that sets the image size on the TileSet
            TileSet.ImageSize = TextureAtlas.Texture.ContentSizeInPixels;

            // By default all the tiles are aliased
            // pros:
            //  - easier to render
            // cons:
            //  - difficult to scale / rotate / etc.
            TextureAtlas.IsAntialiased = false;

            // Parse cocos2d properties
            ParseInternalProperties();

            for (int y = 0; y < LayerSize.Height; y++)
            {
                for (int x = 0; x < LayerSize.Width; x++)
                {
                    var pos = (int) (x + LayerSize.Width * y);
                    uint gid = Tiles[pos];

                    // gid are stored in little endian.
                    // if host is big endian, then swap
                    //if( o == CFByteOrderBigEndian )
                    //    gid = CFSwapInt32( gid );
                    /* We support little endian.*/

                    // XXX: gid == 0 -. empty tile
                    if (gid != 0)
                    {
                        AppendTileForGID(gid, new CCPoint(x, y));

                        // Optimization: update min and max GID rendered by the layer
                        minGID = Math.Min(gid, minGID);
                        maxGID = Math.Max(gid, maxGID);
                    }
                }
            }

            Debug.Assert(maxGID >= TileSet.FirstGid &&
                minGID >= TileSet.FirstGid, "TMX: Only 1 tilset per layer is supported");
        }

        // Returned CCSprite will be already added to the CCTMXLayer. Don't add it again.
        public virtual CCSprite TileAt(CCPoint pos)
        {
            return TileAt(pos.X, pos.Y);
        }

        // Returned CCSprite will be already added to the CCTMXLayer. Don't add it again.
        public virtual CCSprite TileAt(float x, float y)
        {
            Debug.Assert(x < LayerSize.Width && y < LayerSize.Height && x >= 0 && y >= 0, "TMXLayer: invalid position");
            Debug.Assert(Tiles != null && atlasIndexArray != null, "TMXLayer: the tiles map has been released");

            CCSprite tile = null;
            uint gid = TileGIDAt(x, y);

            // No tile is present
            if (gid != 0)
            {
                var z = (int) (x + y * LayerSize.Width);
                tile = (CCSprite) this[z];

                if (tile == null)
                {
                    CCRect rect = TileSet.RectForGID(gid);

                    tile = new CCSprite(Texture, rect);
                    tile.BatchNode = this;
                    tile.Position = PositionAt(x, y);
                    tile.VertexZ = VertexZForPos(x, y);
                    tile.AnchorPoint = CCPoint.Zero;
                    tile.Opacity = opacity;

                    int indexForZ = AtlasIndexForExistantZ(z);
                    AddSpriteWithoutQuad(tile, indexForZ, z);
                }
            }

            return tile;
        }

        public virtual uint TileGIDAt(CCPoint pos)
        {
            uint tmp;
            return TileGIDAt(pos, out tmp);
        }

        public virtual uint TileGIDAt(float x, float y)
        {
            uint tmp;
            return TileGIDAt(x, y, out tmp);
        }

        public virtual uint TileGIDAt(CCPoint pos, out uint flags)
        {
            Debug.Assert(pos.X < LayerSize.Width && pos.Y < LayerSize.Height && pos.X >= 0 && pos.Y >= 0, "TMXLayer: invalid position");
            Debug.Assert(Tiles != null && atlasIndexArray != null, "TMXLayer: the tiles map has been released");

            return TileGIDAt(pos.X, pos.Y, out flags);
        }

        public virtual uint TileGIDAt(float x, float y, out uint flags)
        {
            Debug.Assert(x < LayerSize.Width && y < LayerSize.Height && x >= 0 && y >= 0, "TMXLayer: invalid position");
            Debug.Assert(Tiles != null && atlasIndexArray != null, "TMXLayer: the tiles map has been released");

            var idx = (int) (x + y * LayerSize.Width);
            // Bits on the far end of the 32-bit global tile ID are used for tile flags
            uint tile = Tiles[idx];

            // issue1264, flipped tiles can be changed dynamically
            flags = (tile & CCTMXTileFlags.FlippedAll);

            return (tile & CCTMXTileFlags.FlippedMask);
        }

        // If a tile is already placed at that position, then it will be removed.
        public virtual void SetTileGID(uint gid, CCPoint pos)
        {
            SetTileGID(gid, pos, 0);
        }

        public virtual void SetTileGID(uint gid, CCPoint pos, uint flags)
        {
            Debug.Assert(pos.X < LayerSize.Width && pos.Y < LayerSize.Height && pos.X >= 0 && pos.Y >= 0, "TMXLayer: invalid position");
            Debug.Assert(Tiles != null && atlasIndexArray != null, "TMXLayer: the tiles map has been released");
            Debug.Assert(gid == 0 || gid >= TileSet.FirstGid, "TMXLayer: invalid gid");

            uint currentFlags;
            uint currentGID = TileGIDAt(pos, out currentFlags);

            if (currentGID != gid || currentFlags != flags)
            {
                uint gidAndFlags = gid | flags;

                // setting gid=0 is equal to remove the tile
                if (gid == 0)
                {
                    RemoveTileAt(pos);
                }
                // empty tile. create a new one
                else if (currentGID == 0)
                {
                    InsertTileForGID(gidAndFlags, pos);
                }
                // modifying an existing tile with a non-empty tile
                else
                {
                    var z = (int) (pos.X + pos.Y * LayerSize.Width);
                    var sprite = (CCSprite) this[z];
                    if (sprite != null)
                    {
                        CCRect rect = TileSet.RectForGID(gid);

                        sprite.ContentSize = rect.Size / DefaultTexelToContentSizeRatios;
                        sprite.TextureRectInPixels = rect;
                        sprite.IsTextureRectRotated = false;

                        if (flags != 0)
                        {
                            SetupTileSprite(sprite, sprite.Position, gidAndFlags);
                        }
                        Tiles[z] = gidAndFlags;
                    }
                    else
                    {
                        UpdateTileForGID(gidAndFlags, pos);
                    }
                }
            }
        }

        public virtual void RemoveTileAt(CCPoint pos)
        {
            Debug.Assert(pos.X < LayerSize.Width && pos.Y < LayerSize.Height && pos.X >= 0 && pos.Y >= 0, "TMXLayer: invalid position");
            Debug.Assert(Tiles != null && atlasIndexArray != null, "TMXLayer: the tiles map has been released");

            uint gid = TileGIDAt(pos);

            if (gid != 0)
            {
                var z = (int) (pos.X + pos.Y * LayerSize.Width);
                int atlasIndex = AtlasIndexForExistantZ(z);

                // remove tile from GID map
                Tiles[z] = 0;

                // remove tile from atlas position array
                atlasIndexArray.RemoveAt(atlasIndex);

                // remove it from sprites and/or texture atlas
                var sprite = (CCSprite) this[z];
                if (sprite != null)
                {
                    base.RemoveChild(sprite, true);
                }
                else
                {
                    TextureAtlas.RemoveQuadAtIndex(atlasIndex);

                    // update possible children
                    if (Children != null && Children.Count > 0)
                    {
                        CCNode[] elements = Children.Elements;
                        int count = Children.Count;

                        for (int i = 0; i < count; i++)
                        {
                            var pChild = (CCSprite) elements[i];
                            int ai = pChild.AtlasIndex;
                            if (ai >= atlasIndex)
                            {
                                pChild.AtlasIndex = (ai - 1);
                            }
                        }
                    }
                }
            }
        }

        public virtual CCPoint PositionAt(CCPoint tileCoord)
        {
            return PositionAt(tileCoord.X, tileCoord.Y);
        }

        public virtual CCPoint PositionAt(float x, float y)
        {
            CCPoint ret = CCPoint.Zero;
            switch (LayerOrientation)
            {
            case CCTMXOrientation.Ortho:
                ret = PositionForOrthoAt(x, y);
                break;
            case CCTMXOrientation.Iso:
                ret = PositionForIsoAt(x, y);
                break;
            case CCTMXOrientation.Hex:
                ret = PositionForHexAt(x, y);
                break;
            }

            return ret;
        }

        CCPoint PositionForIsoAt(CCPoint tileCoord)
        {
            return PositionForIsoAt(tileCoord.X, tileCoord.Y);
        }

        CCPoint PositionForIsoAt(float x, float y)
        {
            var xy = new CCPoint(MapTileSize.Width / 2 * (LayerSize.Width + x - y - 1),
                MapTileSize.Height / 2 * ((LayerSize.Height * 2 - x - y) - 2));
            return xy;
        }

        CCPoint PositionForOrthoAt(CCPoint tileCoord)
        {
            return PositionForOrthoAt(tileCoord.X, tileCoord.Y);
        }

        CCPoint PositionForOrthoAt(float x, float y)
        {
            CCPoint xy = new CCPoint(x * MapTileSize.Width,
                (LayerSize.Height - y - 1) * MapTileSize.Height);
            return xy;
        }

        CCPoint PositionForHexAt(CCPoint tileCoord)
        {
            return PositionForHexAt(tileCoord.X, tileCoord.Y);
        }

        CCPoint PositionForHexAt(float x, float y)
        {
            float diffY = 0;
            if ((int) x % 2 == 1)
            {
                diffY = -MapTileSize.Height / 2;
            }

            var xy = new CCPoint(x * MapTileSize.Width * 3 / 4,
                (LayerSize.Height - y - 1) * MapTileSize.Height + diffY);
            return xy;
        }

        CCPoint ApplyLayerOffset(CCPoint tileCoord)
        {
            CCPoint ret = CCPoint.Zero;
            switch (LayerOrientation)
            {
            case CCTMXOrientation.Ortho:
                ret = new CCPoint(tileCoord.X * MapTileSize.Width, -tileCoord.Y * MapTileSize.Height);
                break;
            case CCTMXOrientation.Iso:
                ret = new CCPoint((MapTileSize.Width / 2) * (tileCoord.X - tileCoord.Y),
                    (MapTileSize.Height / 2) * (-tileCoord.X - tileCoord.Y));
                break;
            case CCTMXOrientation.Hex:
                Debug.Assert(tileCoord.Equals(CCPoint.Zero), "offset for hexagonal map not implemented yet");
                break;
            }
            return ret;
        }

        CCSprite AppendTileForGID(uint gid, CCPoint tileCoord)
        {
            CCRect rect = TileSet.RectForGID(gid);

            var z = (int) (tileCoord.X + tileCoord.Y * LayerSize.Width);

            CCSprite tile = ReusedTileWithRect(rect);

            SetupTileSprite(tile, tileCoord, gid);

            // optimization:
            // The difference between appendTileForGID and insertTileforGID is that append is faster, since
            // it appends the tile at the end of the texture atlas
            int indexForZ = atlasIndexArray.Count;

            // don't add it using the "standard" way.
            InsertQuadFromSprite(tile, indexForZ);

            // append should be after addQuadFromSprite since it modifies the quantity values
            atlasIndexArray.Insert(indexForZ, z);

            return tile;
        }

        CCSprite InsertTileForGID(uint gid, CCPoint tileCoord)
        {
            CCRect rect = TileSet.RectForGID(gid);

            var z = (int) (tileCoord.X + tileCoord.Y * LayerSize.Width);

            CCSprite tile = ReusedTileWithRect(rect);

            SetupTileSprite(tile, tileCoord, gid);

            // get atlas index
            int indexForZ = AtlasIndexForNewZ(z);

            // Optimization: add the quad without adding a child
            InsertQuadFromSprite(tile, indexForZ);

            // insert it into the local atlasindex array
            atlasIndexArray.Insert(indexForZ, z);

            // update possible children
            if (Children != null && Children.Count > 0)
            {
                CCNode[] elements = Children.Elements;
                int count = Children.Count;

                for (int i = 0; i < count; i++)
                {
                    var sprite = (CCSprite) elements[i];
                    int ai = sprite.AtlasIndex;
                    if (ai >= indexForZ)
                    {
                        sprite.AtlasIndex = ai + 1;
                    }
                }
            }
            Tiles[z] = gid;
            return tile;
        }

        CCSprite UpdateTileForGID(uint gid, CCPoint tileCoord)
        {
            CCRect rect = TileSet.RectForGID(gid);
            var z = (int) (tileCoord.X + tileCoord.Y * LayerSize.Width);

            CCSprite tile = ReusedTileWithRect(rect);

            SetupTileSprite(tile, tileCoord, gid);

            // get atlas index
            int indexForZ = AtlasIndexForExistantZ(z);
            tile.AtlasIndex = indexForZ;
            tile.UpdateTransformedSpriteTextureQuads();
            Tiles[z] = gid;

            return tile;
        }

        void SetupTileSprite(CCSprite sprite, CCPoint tileCoord, uint gid)
        {
            sprite.Position = PositionAt(tileCoord);
            sprite.VertexZ = VertexZForPos(tileCoord);
            sprite.AnchorPoint = CCPoint.Zero;
            sprite.Opacity = opacity;

            // issue 1264, flip can be undone as well
            sprite.FlipX = false;
            sprite.FlipY = false;
            sprite.Rotation = 0.0f;
            sprite.AnchorPoint = CCPoint.Zero;

            // Rotation in tiled is achieved using 3 flipped states, flipping across the horizontal, vertical, and diagonal axes of the tiles.
            if ((gid & CCTMXTileFlags.TileDiagonal) != 0)
            {
                // put the anchor in the middle for ease of rotation.
                sprite.AnchorPoint = CCPoint.AnchorMiddle;
                CCPoint pointAtPos = PositionAt(tileCoord);
                sprite.Position = new CCPoint(pointAtPos.X + sprite.ContentSize.Height / 2,
                    pointAtPos.Y + sprite.ContentSize.Width / 2);

                uint flag = gid & (CCTMXTileFlags.Horizontal | CCTMXTileFlags.Vertical);

                // handle the 4 diagonally flipped states.
                if (flag == CCTMXTileFlags.Horizontal)
                {
                    sprite.Rotation = 90.0f;
                }
                else if (flag == CCTMXTileFlags.Vertical)
                {
                    sprite.Rotation = 270.0f;
                }
                else if (flag == (CCTMXTileFlags.Vertical | CCTMXTileFlags.Horizontal))
                {
                    sprite.Rotation = 90.0f;
                    sprite.FlipX = true;
                }
                else
                {
                    sprite.Rotation = 270.0f;
                    sprite.FlipX = true;
                }
            }
            else
            {
                if ((gid & CCTMXTileFlags.Horizontal) != 0)
                {
                    sprite.FlipX = true;
                }

                if ((gid & CCTMXTileFlags.Vertical) != 0)
                {
                    sprite.FlipY = true;
                }
            }
        }

        CCSprite ReusedTileWithRect(CCRect rect)
        {
            CCSize contentSize = rect.Size / DefaultTexelToContentSizeRatios;

            if (reusedTile == null)
            {
                reusedTile = new CCSprite(TextureAtlas.Texture, rect, false);
                reusedTile.ContentSize = contentSize;
                reusedTile.BatchNode = this;
            }
            else
            {
                // XXX HACK: Needed because if "batch node" is nil,
                // then the Sprite'squad will be reset
                reusedTile.BatchNode = null;

                // Re-init the sprite
                reusedTile.ContentSize = contentSize;
                reusedTile.IsTextureRectRotated = false;
                reusedTile.TextureRectInPixels = rect;

                // restore the batch node
                reusedTile.BatchNode = this;
            }

            return reusedTile;
        }

        int VertexZForPos(CCPoint pos)
        {
            return VertexZForPos(pos.X, pos.Y);
        }

        int VertexZForPos(float x, float y)
        {
            int ret = 0;
            if (useAutomaticVertexZ)
            {
                switch (LayerOrientation)
                {
                case CCTMXOrientation.Iso:
                    var maxVal = (int) (LayerSize.Width + LayerSize.Height);
                    ret = (int) (-(maxVal - (x + y)));
                    break;
                case CCTMXOrientation.Ortho:
                    ret = (int) (-(LayerSize.Height - y));
                    break;
                case CCTMXOrientation.Hex:
                    Debug.Assert(false, "TMX Hexa zOrder not supported");
                    break;
                default:
                    Debug.Assert(false, "TMX invalid value");
                    break;
                }
            }
            else
            {
                ret = vertexZvalue;
            }

            return ret;
        }

        #endregion Tile management
    }
}