/*
 * @author SlipperSoar
 * @Created: 2024-12-23
 * @description Gif动图解码
 * @reference https://blog.csdn.net/wzy198852/article/details/17266507
 */

using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

namespace SS.UIComponent
{
    #region inner class

    public class GifData
    {
        public float DelaySecond { get; private set; }
        
        public Texture2D FrameTexture { get; private set; }

        public GifData(float delaySecond, Texture2D frameTexture)
        {
            DelaySecond = delaySecond;
            FrameTexture = frameTexture;
        }
    }
    
    public interface IGifExtensionBlock
    {
        byte BlockFlag { get; }
    }
    
    /// <summary>
    /// 图形扩展块
    /// </summary>
    public class GifGraphicExtensionBlock : IGifExtensionBlock
    {
        public byte BlockFlag => GifDecoder.GIF_GRAPHIC_CONTROL_FLAG;
        
        /// <summary>处置方法</summary>
        public GifDecoder.GraphicExecuteMethod ExecuteMethod { get; }
        
        /// <summary>是否使用透明色</summary>
        public bool TransparentColorFlag { get; }
        
        /// <summary>透明色索引</summary>
        public byte TransparentColorIndex { get; }
        
        /// <summary>单位是1/100秒</summary>
        public ushort DelayTime { get; }

        public GifGraphicExtensionBlock(GifDecoder.GraphicExecuteMethod executeMethod, bool transparentColorFlag, byte transparentColorIndex, ushort delayTime)
        {
            ExecuteMethod = executeMethod;
            TransparentColorFlag = transparentColorFlag;
            TransparentColorIndex = transparentColorIndex;
            DelayTime = delayTime;
        }
    }

    /// <summary>
    /// 注释扩展块
    /// </summary>
    public class GifCommentExtensionBlock : IGifExtensionBlock
    {
        private static GifCommentExtensionBlock _emptyBlock;
        public static GifCommentExtensionBlock EmptyBlock
        {
            get
            {
                if (_emptyBlock == null)
                {
                    _emptyBlock = new GifCommentExtensionBlock();
                }

                return _emptyBlock;
            }
        }

        public byte BlockFlag => GifDecoder.GIF_COMMENT_FLAG;
    }
    
    /// <summary>
    /// 文本扩展块
    /// </summary>
    public class GifPlainTextExtensionBlock : IGifExtensionBlock
    {
        public byte BlockFlag => GifDecoder.GIF_PLAIN_TEXT_FLAG;

        /// <summary>文本内容</summary>
        public string Content { get; }
        
        /// <summary>文本框宽度</summary>
        public ushort Width { get; }
        
        /// <summary>文本框高度</summary>
        public ushort Height { get; }
        
        /// <summary>文本框左侧距离</summary>
        public ushort LeftEdge { get; }
        
        /// <summary>文本框顶部距离</summary>
        public ushort TopEdge { get; }
        
        /// <summary>字符单元格宽度</summary>
        public byte CharacterCellWidth { get; }
        
        /// <summary>字符单元格高度</summary>
        public byte CharacterCellHeight { get; }
        
        /// <summary>前景色索引</summary>
        public byte ForegroundColorIndex { get; }
        
        /// <summary>背景色索引</summary>
        public byte BackgroundColorIndex { get; }

        public GifPlainTextExtensionBlock(string content, ushort width, ushort height, ushort leftEdge, ushort topEdge, byte characterCellWidth, byte characterCellHeight, byte foregroundColorIndex, byte backgroundColorIndex)
        {
            Content = content;
            Width = width;
            Height = height;
            LeftEdge = leftEdge;
            TopEdge = topEdge;
            CharacterCellWidth = characterCellWidth;
            CharacterCellHeight = characterCellHeight;
            ForegroundColorIndex = foregroundColorIndex;
            BackgroundColorIndex = backgroundColorIndex;
        }
    }
    
    /// <summary>
    /// 应用程序扩展块
    /// </summary>
    public class GifApplicationExtensionBlock : IGifExtensionBlock
    {
        private static GifApplicationExtensionBlock _emptyBlock;
        public static GifApplicationExtensionBlock EmptyBlock
        {
            get
            {
                if (_emptyBlock == null)
                {
                    _emptyBlock = new GifApplicationExtensionBlock();
                }

                return _emptyBlock;
            }
        }

        public byte BlockFlag => GifDecoder.GIF_APPLICATION_FLAG;
    }

    /// <summary>
    /// 图像数据块
    /// </summary>
    public class ImageBlock
    {
        public byte[] ImageData;
        public ushort Width;
        public ushort Height;
        public ushort XOffset;
        public ushort YOffset;
        public bool UseInterlace;
        public Color32[] LocalColorTable;
    }
    
    #endregion
    
    public static class GifDecoder
    {
        #region enum

        /// <summary>
        /// 图形处置方法的类型
        /// </summary>
        public enum GraphicExecuteMethod
        {
            /// <summary>不使用处置方法，保留当前图像，直到显示下一帧</summary>
            None,
            /// <summary>不移除当前帧的图像内容，下一帧会直接覆盖在当前帧的基础上</summary>
            DoNotDispose,
            /// <summary>在显示下一帧之前，将当前帧占用的区域填充为背景色</summary>
            RecoveryByBgColor,
            /// <summary>在显示下一帧之前，恢复到当前帧显示之前的画面状态</summary>
            RecoveryToPrev,
            /// <summary>自定义方式1</summary>
            Custom1,
            /// <summary>自定义方式2</summary>
            Custom2,
            /// <summary>自定义方式3</summary>
            Custom3,
            /// <summary>自定义方式4</summary>
            Custom4,
        }

        #endregion

        #region constant

        /// <summary>Gif图像标识符的起始字节</summary>
        public const byte GIF_SIGNATURE = 0x2C;

        /// <summary>gif文件内容结束标识符</summary>
        public const byte GIF_END_SIGNATURE = 0x3B;
        
        /// <summary>扩展块标识</summary>
        public const byte GIF_Extension_BLOCK_FLAG = 0x21;

        /// <summary>扩展中的图形控制标识</summary>
        public const byte GIF_GRAPHIC_CONTROL_FLAG = 0xF9;

        /// <summary>扩展中的注释标识</summary>
        public const byte GIF_COMMENT_FLAG = 0xFE;

        /// <summary>扩展中的文本标识</summary>
        public const byte GIF_PLAIN_TEXT_FLAG = 0x01;

        /// <summary>扩展中的应用程序自定义标识</summary>
        public const byte GIF_APPLICATION_FLAG = 0xFF;

        /// <summary>块终结标识</summary>
        public const byte GIF_BLOCK_END_FLAG = 0;

        /// <summary>最大编码位数</summary>
        public const byte MAX_BIT_CODE_COUNT = 12;
        
        /// <summary>编码表最大大小</summary>
        public const int MAX_CODE_TABLE_SIZE = 1 << MAX_BIT_CODE_COUNT;

        #endregion
        
        /// <summary>
        /// 解码GIF
        /// </summary>
        /// <param name="bytes">Gif文件的字节数据</param>
        /// <param name="onComplete">解码完成的回调，参数为帧数据</param>
        /// <returns>迭代器，可用于协程，避免主线程阻塞</returns>
        public static IEnumerator Decode(byte[] bytes, Action<List<GifData>> onComplete)
        {
            var frames = new List<GifData>();
            var byteIndex = 0;
            // Header
            var sb = new StringBuilder();
            for (; byteIndex < 6; byteIndex++)
            {
                sb.Append((char)bytes[byteIndex]);
            }
            
            // Header一定是GIF
            var header = sb.ToString();
            if (header[0] != 'G' || header[1] != 'I' || header[2] != 'F')
            {
#if UNITY_EDITOR
                Debug.LogError($"This is not a GIF!");
#endif
                yield break;
            }

            // 版本：87a或89a
            // var version = header.Substring(3, 3);
#if UNITY_EDITOR
            Debug.Log($"Header: {header}");
#endif
            
            // 逻辑屏幕标识符
            // 2字节的宽度信息
            var screenWidth = BitConverter.ToUInt16(bytes, byteIndex);
            byteIndex += 2;
            // 2字节的高度信息
            var screenHeight = BitConverter.ToUInt16(bytes, byteIndex);
            byteIndex += 2;
            // 一字节的四个数据标志
            var flagByte = bytes[byteIndex++];
            // 第一位：是否存在全局颜色表
            var globalColorTableFlag = (flagByte & 0b10000000) != 0;
            // 第2-4位：颜色深度
            var colorResolution = (flagByte & 0b01110000) >> 4 + 1;
            // 第5位：是否排序
            var sortFlag = (flagByte & 0b00001000) != 0;
            // 第6-8位：全局颜色表大小
            var globalColorTableSize = 2 << (flagByte & 0b00000111);
            // 背景色
            var backgroundColorIndex = bytes[byteIndex++];
            // 像素宽高比
            var pixelAspectRatio = bytes[byteIndex++];
#if UNITY_EDITOR
            Debug.Log($"Screen: {screenWidth}x{screenHeight} flag byte: {flagByte}, globalColorTableFlag: {globalColorTableFlag}, colorResolution: {colorResolution}, sortFlag: {sortFlag}, globalColorTableSize: {globalColorTableSize}, backgroundColorIndex: {backgroundColorIndex}, pixelAspectRatio: {pixelAspectRatio}");
#endif
            
            // 当全局颜色表不存在时，背景色用Clear色（无色）
            var bgColor = new Color32(0, 0, 0, 0);
            // 全局颜色表
            Color32[] globalColorTable = null;
            if (globalColorTableFlag)
            {
                globalColorTable = new Color32[globalColorTableSize];
                for (int i = 0; i < globalColorTableSize; i++)
                {
                    var r = bytes[byteIndex++];
                    var g = bytes[byteIndex++];
                    var b = bytes[byteIndex++];
                    globalColorTable[i] = new Color32(r, g, b, 255);
                }

                bgColor = globalColorTable[backgroundColorIndex];
#if UNITY_EDITOR
                Debug.Log($"GlobalColorTable Length: {globalColorTable.Length}, background color: {globalColorTable[backgroundColorIndex]}");
#endif
            }

            yield return null;
            
            // 图像块标识符
            IGifExtensionBlock extensionBlock = null;
            Color32[] prevColors = null;
            var screenSize = screenWidth * screenHeight;
            while (bytes[byteIndex] != GIF_END_SIGNATURE)
            {
                var blockFlag = bytes[byteIndex++];
                if (blockFlag == GIF_SIGNATURE)
                {
                    var imageBlock = ReadImageDataBlock(bytes, screenSize, ref byteIndex);
                    if (imageBlock.UseInterlace)
                    {
#if UNITY_EDITOR
                        Debug.Log($"Decode interlace data");
#endif
                        imageBlock.ImageData= ProcessInterlaceData(imageBlock, screenWidth, screenHeight);
                    }
                    
                    yield return null;
                    
                    var texture = new Texture2D(screenWidth, screenHeight, TextureFormat.ARGB32, false);
                    var usingColorTable = imageBlock.LocalColorTable ?? globalColorTable;
                    if (usingColorTable == null)
                    {
                        // TODO: 使用系统颜色表
                    }

                    var frame = ProcessTexturePixels(texture, imageBlock, bgColor, extensionBlock as GifGraphicExtensionBlock,
                        usingColorTable, prevColors, out var colors);
                    frames.Add(frame);
                    yield return null;

                    prevColors = colors;
                }
                else if (blockFlag == GIF_Extension_BLOCK_FLAG) // 扩展块，会影响接下来一个图像数据块
                {
                    extensionBlock = ReadExtensionBlock(bytes, ref byteIndex);
                }
                else
                {
#if UNITY_EDITOR
                    Debug.Log($"unknown block flag: {blockFlag}({blockFlag:x2})");
#endif
                }
            }
            
            onComplete?.Invoke(frames);
        }

        #region Private Methods

        private static ImageBlock ReadImageDataBlock(byte[] bytes, int screenSize, ref int byteIndex)
        {
            Color32[] localColorTable = null;
            // 每个图像块：1字节标识符 + 2字节 x offset + 2字节 y offset  + 2字节宽度 + 2字节高度 + 1字节标志（像上面逻辑屏幕标识符一样）
            var xOffset = BitConverter.ToUInt16(bytes, byteIndex);
            byteIndex += 2;
            var yOffset = BitConverter.ToUInt16(bytes, byteIndex);
            byteIndex += 2;
            var width = BitConverter.ToUInt16(bytes, byteIndex);
            byteIndex += 2;
            var height = BitConverter.ToUInt16(bytes, byteIndex);
            byteIndex += 2;
            var flagByte = bytes[byteIndex++];
            var localColorTableFlag = (flagByte & 0b10000000) != 0;
            var interlaceFlag = (flagByte & 0b01000000) != 0;
            var sortFlag = (flagByte & 0b00100000) != 0;
            var localColorTableSize = 2 << (flagByte & 0b00000111);
#if UNITY_EDITOR
            Debug.Log($"Image: ({xOffset},{yOffset}), {width}x{height}, localColorTableFlag: {localColorTableFlag}, interlaceFlag: {interlaceFlag}, sortFlag: {sortFlag}, localColorTableSize: {localColorTableSize}");
#endif

            var imageSize = width * height;
            
            // 局部颜色表
            if (localColorTableFlag)
            {
                // 使用局部颜色表
                localColorTable = new Color32[localColorTableSize];
                for (int i = 0; i < localColorTableSize; i++)
                {
                    var r = bytes[byteIndex++];
                    var g = bytes[byteIndex++];
                    var b = bytes[byteIndex++];
                    localColorTable[i] = new Color32(r, g, b, 255);
                }
                
#if UNITY_EDITOR
                Debug.Log($"LocalColorTable Length: {localColorTable.Length}");
#endif
            }

            // LZW压缩
            var lzwMinCodeSize = bytes[byteIndex++];
            var lzwSize = 1 << lzwMinCodeSize;
#if UNITY_EDITOR
            Debug.Log($"LZW Size: {lzwSize}");
#endif
            var clearCode = lzwSize;
            
            // 读取所有的子数据块并合并成完整的数据流
            var totalData = new List<byte>();
            var totalSize = 0;
            while (true)
            {
                if (byteIndex >= bytes.Length)
                {
#if UNITY_EDITOR
                    Debug.LogError($"index out of range, byteIndex: {byteIndex}, bytes.Length: {bytes.Length}");
#endif
                    break;
                }
                var size = bytes[byteIndex++];
                if (size == GIF_BLOCK_END_FLAG)
                {
                    break;
                }

                totalSize += size;
                totalData.Capacity = totalSize;
                for (int i = 0; i < size; i++)
                {
                    totalData.Add(bytes[byteIndex++]);
                }
            }

            // 将整个字节数据流转换成bit数据流并用于解码
            var bitData = new BitArray(totalData.ToArray());
            var decodedBytes = DecodeImageBlockData(bitData, clearCode, lzwMinCodeSize, imageSize);
#if UNITY_EDITOR
            Debug.Log($"decoded bytes: {decodedBytes?.Length}");
#endif

            return new ImageBlock()
            {
                ImageData = decodedBytes,
                Width = width,
                Height = height,
                XOffset = xOffset,
                YOffset = yOffset,
                UseInterlace = interlaceFlag,
                LocalColorTable = localColorTable
            };
        }

        private static byte[] DecodeImageBlockData(BitArray imageData, int clearCode, int lzwMinCodeSize, int imageSize)
        {
            // key是索引，value是像素颜色在颜色表中的索引，每个索引用一个byte表示，这里用char组成的string比较方便
            var codeTable = new Dictionary<int, string>();

            var output = new byte[imageSize];
            var outputIndex = 0;
            // 起始读取位大小
            int codeSize;
            var endCode = clearCode + 1;
            // 当前所能表示的最大表大小
            int maxTableSize;
            var bitIndex = 0;

            string prevStr;
            var codeStr = string.Empty;
            
            void InitTable()
            {
                codeTable.Clear();
                // 初始化编码表
                for (int i = 0; i < clearCode + 2; i++)
                {
                    codeTable[i] = ((char)i).ToString();
                }

                codeSize = lzwMinCodeSize + 1;
                maxTableSize = 1 << codeSize;
                prevStr = string.Empty;
            }
            
            InitTable();
            
#if UNITY_EDITOR
            Debug.Log($"[LZW Decode] Clear Code: {clearCode}, code size (lzw min code size + 1): {codeSize}");
#endif
            
            while (bitIndex < imageData.Length)
            {
                // 从bit数组中按照size大小读取对应长度并转换成数值
                // Gif使用低位在前，高位在后（从右往左）的存储顺序
                var value = imageData.GetValue(ref bitIndex, codeSize);
                
                // clearCode表示重置编码表
                if (value == clearCode)
                {
#if UNITY_EDITOR
                    Debug.Log($"[LZW Decode] Code Table Reset With Clear Code, code table size: {codeTable.Count}, code size: {codeSize}");
#endif
                    InitTable();
                    continue;
                }
                // clearCode + 1表示结束标志
                else if (value == endCode)
                {
#if UNITY_EDITOR
                    Debug.Log($"[LZW Decode] Decode LZW End With:  clear code + 1");
#endif
                    break;
                }
                else if (codeTable.ContainsKey(value))
                {
                    codeStr = codeTable[value];
                    // 获取当前code对应的字符
                    // 编码表包含当前编码
                    // 获取当前code对应的字符
                    for (int i = 0; i < codeStr.Length; i++)
                    {
                        output[outputIndex++] = (byte)codeStr[i];
                    }
                    
                    // 将当前字符添加到编码表中
                    if (!string.IsNullOrEmpty(prevStr))
                    {
                        codeTable.Add(codeTable.Count, prevStr + codeStr[0]);
                    }
                }
                // 编码表不包含当前编码
                else
                {
                    // 如果没有在编码表中找到，说明在编码时，这个prefix是被前一步新加进编码表的
                    // 而这个新的编码是由上一步输出完索引后， 将prefix+c添加进的编码表，然后将prefix = 新字符
                    // 在这之后接着产生这个找不到的索引，它对应的“编码时”prefix的首个字符应当和之前的prefix的首个字符相同，否则不会是编码表中的最新编码
                    if (!string.IsNullOrEmpty(prevStr))
                    {
                        codeStr = prevStr + prevStr[0];
                        for (int i = 0; i < codeStr.Length; i++)
                        {
                            output[outputIndex++] = (byte)codeStr[i];
                        }
                        // 将当前字符添加到编码表中
                        codeTable.Add(codeTable.Count, codeStr);
                    }
                    else
                    {
#if UNITY_EDITOR
                        Debug.LogError($"[LZW Decode] Should not happen, index value: {value}, code table size: {codeTable.Count}, lzw min size: {lzwMinCodeSize}, code size: {codeSize}");
#endif
                        continue;
                    }
                }

                prevStr = codeStr;

                // 数据够了
                if (outputIndex >= imageSize)
                {
                    break;
                }

                // if (codeTable.Count >= MAX_CODE_TABLE_SIZE)
                // {
                //     value = imageData.GetValue(ref bitIndex, codeSize);
                //     if (value != clearCode)
                //     {
                //         Debug.Log($"[LZW Decode] Reset Code Table Because Code Table Size >= Max Table Size, but value is not clear code: {value}, code table size: {codeTable.Count}, code size: {codeSize}, max table size: {maxTableSize}");
                //         InitTable();
                //     }
                //     else
                //     {
                //         bitIndex -= codeSize;
                //     }
                //
                //     continue;
                // }

                // 检查并根据情况调整codeSize
                if (codeTable.Count >= maxTableSize && codeSize < 12)
                {
                    codeSize++;
                    maxTableSize = 1 << codeSize;
#if UNITY_EDITOR
                    Debug.Log($"[LZW Decode] Code Table Size Increase, code table size: {codeTable.Count}, code size: {codeSize}, next max table size: {maxTableSize}");
#endif
                }

                // Debug.Log($"Code: {value}, table count: {codeTable.Count}, bit index: {bitIndex}, data length: {imageData.Length}, code size: {codeSize}");
            }

            return output;
        }
        
        private static byte[] ProcessInterlaceData(ImageBlock imageBlock, ushort screenWidth, ushort screenHeight)
        {
            byte[] bytes = imageBlock.ImageData;
            ushort width = imageBlock.Width;
            ushort height = imageBlock.Height;
            // 高度从1开始的，这里要从0开始才好统一计算
            // x: 行数索引, height: 原始图像总行数, y: 通道总行数
            // 第一通道(Pass 1)提取从第0行开始每隔8行的数据；8x, y = Min(1, height / 8)
            // 第二通道(Pass 2)提取从第4行开始每隔8行的数据；8x+4, y = height < 5 ? 0 : (height - 4) / 8
            // 第三通道(Pass 3)提取从第2行开始每隔4行的数据；4x+2, y = height < 3 ? 0 : (height - 2) / 4
            // 第四通道(Pass 4)提取从第1行开始每隔2行的数据；2x+1, y = height < 2 ? 0 : (height - 1) / 2
            
            // 也就是说，图像数据按照行数交织，所以也按行数进行数据交换
            // 交织：数据按照通道顺序存储，需要从通道转换到原始的行
            var dataCount = bytes.Length;
            // 根本不超过一行，就不用处理了
            if (dataCount <= width)
            {
                return bytes;
            }

            var newBytes = new byte[dataCount];
            // 每个通道的截止行数
            var pass1 = Mathf.Min(1, height / 8);
            var pass2 = pass1 + (height < 5 ? 0 : (height - 4) / 8);
            var pass3 = pass2 + (height < 3 ? 0 : (height - 2) / 4);
            var pass4 = pass3 + (height < 2 ? 0 : (height - 1) / 2);
            for (int i = 0; i < dataCount; i++)
            {
                // lineCount * width + [0, width - 1] => lineCount + 0
                var lineCount = i / width;
                var lineIndex = i - lineCount * width;
                // 通道1：每隔8行存储一次数据，从0开始
                if (lineCount < pass1)
                {
                    // Pass 1
                    lineCount = 8 * lineCount;
                }
                else if (lineCount < pass2)
                {
                    // Pass 2
                    lineCount = 8 * lineCount + 4;
                }
                else if (lineCount < pass3)
                {
                    // Pass 3
                    lineCount = 4 * lineCount + 2;
                }
                else if (lineCount < pass4)
                {
                    // Pass 4
                    lineCount = 2 * lineCount + 1;
                }
                
                newBytes[lineCount * width + lineIndex] = bytes[i];
            }
            
            return newBytes;
        }

        private static GifData ProcessTexturePixels(Texture2D texture2D, ImageBlock imageBlock, Color32 bgColor,
            GifGraphicExtensionBlock graphicExtensionBlock, Color32[] colorTable, Color32[] prevColors, out Color32[] colors)
        {
            var width = texture2D.width;
            var height = texture2D.height;
            var imageSize = width * height;
            var tempColors = new Color32[imageSize];
            var delayTime = 0f;

            void FillTexture(bool lerp = false)
            {
                for (int i = 0; i < height; i++)
                {
                    if (i < imageBlock.YOffset || i >= imageBlock.YOffset + imageBlock.Height)
                    {
                        continue;
                    }

                    for (int j = 0; j < width; j++)
                    {
                        if (j < imageBlock.XOffset || j >= imageBlock.XOffset + imageBlock.Width)
                        {
                            continue;
                        }

                        var index = i * width + j;
                        var indexInBlock = (i - imageBlock.YOffset) * imageBlock.Width + (j - imageBlock.XOffset);
                        // var colorIndex = imageBlock.ImageData[index];
                        var colorIndex = imageBlock.ImageData[indexInBlock];
                        var color = bgColor;
                        if (colorIndex < colorTable.Length)
                        {
                            color = colorTable[colorIndex];
                        }
                        if (graphicExtensionBlock != null && graphicExtensionBlock.TransparentColorFlag && graphicExtensionBlock.TransparentColorIndex == colorIndex)
                        {
                            color.a = 0;
                        }

                        if (lerp)
                        {
                            tempColors[index] = Color32.Lerp(tempColors[index], color, color.a / 0xFF);
                        }
                        else
                        {
                            tempColors[index] = color;
                        }
                    }
                }
            }
            
            if (graphicExtensionBlock == null)
            {
                FillTexture();

                delayTime = 0;
            }
            else
            {
                switch (graphicExtensionBlock.ExecuteMethod)
                {
                    case GraphicExecuteMethod.DoNotDispose:
                    {
                        if (prevColors != null)
                            tempColors = prevColors;
                        else
                        {
#if UNITY_EDITOR
                            Debug.LogWarning($"graphicExtensionBlock.ExecuteMethod is GraphicExecuteMethod.DoNotDispose, but prevColors is null");
#endif
                        }

                        FillTexture(true);
                        break;
                    }
                    case GraphicExecuteMethod.RecoveryByBgColor:
                    {
                        tempColors.FillArray(bgColor);
                        FillTexture();
                    }
                        break;
                    case GraphicExecuteMethod.RecoveryToPrev:
                    {
                        if (prevColors == null)
                        {
#if UNITY_EDITOR
                            Debug.LogWarning($"graphicExtensionBlock.ExecuteMethod is GraphicExecuteMethod.RecoveryToPrev, but prevColors is null");
#endif
                            tempColors.FillArray(bgColor);
                            break;
                        }
                
                        tempColors = prevColors;
                    }
                        break;
                }

                delayTime = graphicExtensionBlock.DelayTime / 100f;
            }

            colors = tempColors;

            // Set Pixels是从下到上的填充，这里需要颠倒一下
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    texture2D.SetPixel(j, height - i - 1, colors[i * width + j]);
                }
            }

            texture2D.Apply();
#if UNITY_EDITOR
            Debug.Log($"Add frame, image block x: {imageBlock.XOffset}, y: {imageBlock.YOffset}, width: {imageBlock.Width}, height: {imageBlock.Height}, delay: {delayTime}");
#endif
            return new GifData(delayTime, texture2D);
        }

        #region Extension Block

        private static IGifExtensionBlock ReadExtensionBlock(byte[] bytes, ref int byteIndex)
        {
            var extensionFlag = bytes[byteIndex++];
            switch (extensionFlag)
            {
                // 注释扩展块
                case GIF_COMMENT_FLAG:
                    return ReadCommentExtensionBlock(bytes, ref byteIndex);
                // 文本扩展块
                case GIF_PLAIN_TEXT_FLAG:
                    return ReadPlainTextExtensionBlock(bytes, ref byteIndex);
                // 图像处理扩展块
                case GIF_GRAPHIC_CONTROL_FLAG:
                    return ReadGraphicExtensionBlock(bytes, ref byteIndex);
                // 应用扩展块
                case GIF_APPLICATION_FLAG:
                    return ReadApplicationExtensionBlock(bytes, ref byteIndex);
            }

            return null;
        }

        private static GifGraphicExtensionBlock ReadGraphicExtensionBlock(byte[] bytes, ref int byteIndex)
        {
            // 块大小，固定4，不包括块终结器
            var blockSize = bytes[byteIndex++];
            // 标识字节，字节不同位置代表不同含义
            var flagByte = bytes[byteIndex++];
            // 处置方法（第4、5、6位）
            var executeMethod = (GraphicExecuteMethod)((flagByte & 0b00011100) >> 2);
            // 4-7的保留字段视为不处理
            switch (executeMethod)
            {
                case GraphicExecuteMethod.Custom1:
                case GraphicExecuteMethod.Custom2:
                case GraphicExecuteMethod.Custom3:
                case GraphicExecuteMethod.Custom4:
                    executeMethod = GraphicExecuteMethod.RecoveryByBgColor;
                    break;
            }

            // 是否等待用户输入（第7位）
            var expectUserInput = (flagByte & 0b00000010) != 0;
            // 是否使用透明色（第8位）
            var transparentColorFlag = (flagByte & 0b00000001) != 0;
#if UNITY_EDITOR
            Debug.Log($"Graphic Control: flag byte: (Binary) {flagByte.ToBinaryString()}, executeMethod: {executeMethod}, expectUserInput: {expectUserInput}, transparentColorFlag: {transparentColorFlag}");
#endif
            
            // 延迟时间，单位1/100 秒，如果值不为1，表示暂停规定的时间后再继续往下处理数据流
            var delayTime = BitConverter.ToUInt16(bytes, byteIndex);
            byteIndex += 2;
            // 透明色索引
            var transparentColorIndex = bytes[byteIndex++];
            // 块终结标识
            var endCode = bytes[byteIndex++];
#if UNITY_EDITOR
            Debug.Log($"graphic extension control block end");
#endif
            return new GifGraphicExtensionBlock(executeMethod, transparentColorFlag, transparentColorIndex, delayTime);
        }

        private static GifPlainTextExtensionBlock ReadPlainTextExtensionBlock(byte[] bytes, ref int byteIndex)
        {
            // 块大小，固定12
            var blockSize = bytes[byteIndex++];
            // 像素值，文本框离逻辑屏幕的左边界距离
            var leftEdge = BitConverter.ToUInt16(bytes, byteIndex);
            byteIndex += 2;
            // 像素值，文本框离逻辑屏幕的上边界距离
            var topEdge = BitConverter.ToUInt16(bytes, byteIndex);
            byteIndex += 2;
            // 文本框宽度像素值
            var width = BitConverter.ToUInt16(bytes, byteIndex);
            byteIndex += 2;
            // 文本框高度像素值
            var height = BitConverter.ToUInt16(bytes, byteIndex);
            byteIndex += 2;
            // 字符单元格宽度
            var characterCellWidth = bytes[byteIndex++];
            // 字符单元格高度
            var characterCellHeight = bytes[byteIndex++];
            // 文本前景色索引
            var foregroundColorIndex = bytes[byteIndex++];
            // 文本背景色索引
            var backgroundColorIndex = bytes[byteIndex++];
            // 要显示的文本
            var sb = new StringBuilder();
            for (;;)
            {
                var byteValue = bytes[byteIndex++];
                if (byteValue == GIF_BLOCK_END_FLAG)
                {
                    break;
                }
                
                sb.Append((char)byteValue);
            }
            
#if UNITY_EDITOR
            Debug.Log($"plain text: {sb}");
#endif
            // TODO: 填充数据
            return new GifPlainTextExtensionBlock(sb.ToString(), width, height, leftEdge, topEdge, characterCellWidth, characterCellHeight, foregroundColorIndex, backgroundColorIndex);
        }
        
        private static GifCommentExtensionBlock ReadCommentExtensionBlock(byte[] bytes, ref int byteIndex)
        {
            // 注释块内容直接跳过
            for (;;)
            {
                var byteValue = bytes[byteIndex++];
                if (byteValue == GIF_BLOCK_END_FLAG)
                {
                    break;
                }
            }

            return GifCommentExtensionBlock.EmptyBlock;
        }
        
        private static GifApplicationExtensionBlock ReadApplicationExtensionBlock(byte[] bytes, ref int byteIndex)
        {
            // 块大小，固定11
            // var blockSize = bytes[byteIndex++];
            // var sb = new StringBuilder();
            // 应用程序标识符，8字符
            // for (int i = 0; i < 8; i++)
            // {
            //     sb.Append((char)bytes[byteIndex++]);
            // }
            //
            // var appFlag = sb.ToString();
            // sb.Clear();
            
            // 应用程序鉴别码，3个字符
            // for (int i = 0; i < 3; i++)
            // {
            //     sb.Append((char)bytes[byteIndex++]);
            // }
            
            // var appAuthenticationCode = sb.ToString();
            // 直接跳过
            byteIndex += 12;

            // 应用程序自定义数据，这里直接跳过
            for (;;)
            {
                var byteValue = bytes[byteIndex++];
                if (byteValue == GIF_BLOCK_END_FLAG)
                {
                    break;
                }
            }

            return GifApplicationExtensionBlock.EmptyBlock;
        }

        #endregion

        #endregion
    }
}