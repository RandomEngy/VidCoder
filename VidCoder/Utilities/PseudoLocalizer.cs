// Copyright (C) Microsoft Corporation. All Rights Reserved.
// This code released under the terms of the Microsoft Public License
// (Ms-PL, http://opensource.org/licenses/ms-pl.html).

#if PSEUDOLOCALIZER_ENABLED

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Media.Imaging;
#if !SILVERLIGHT
using System.Drawing;
#endif

namespace Delay
{
#if !SILVERLIGHT
    /// <summary>
    /// Class that pseudo-localizes resources from a RESX file by intercepting calls to the managed wrapper class.
    /// </summary>
    static class PseudoLocalizer
    {
        /// <summary>
        /// Enables pseudo-localization for the specified RESX managed wrapper class.
        /// </summary>
        /// <param name="resourcesType">Type of the RESX managed wrapper class.</param>
        [SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "ResourceManager", Justification = "Name of property.")]
        [SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "resourceMan", Justification = "Name of field.")]
        public static void Enable(Type resourcesType)
        {
            if (null == resourcesType)
            {
                throw new ArgumentNullException("resourcesType");
            }

            // Get the ResourceManager property
            var resourceManagerProperty = resourcesType.GetProperty("ResourceManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (null == resourceManagerProperty)
            {
                throw new NotSupportedException("RESX managed wrapper class does not contain the expected internal/public static ResourceManager property.");
            }

            // Get the ResourceManager value (ensures the resourceMan field gets initialized)
            var resourceManagerValue = resourceManagerProperty.GetValue(null, null) as ResourceManager;
            if (null == resourceManagerValue)
            {
                throw new NotSupportedException("RESX managed wrapper class returned null for the ResourceManager property getter.");
            }

            // Get the resourceMan field
            var resourceManField = resourcesType.GetField("resourceMan", BindingFlags.Static | BindingFlags.NonPublic);
            if (null == resourceManField)
            {
                throw new NotSupportedException("RESX managed wrapper class does not contain the expected private static resourceMan field.");
            }

            // Create a substitute ResourceManager to do the pseudo-localization
            var resourceManSubstitute = new PseudoLocalizerResourceManager(resourceManagerValue.BaseName, resourcesType.Assembly);

            // Replace the resourceMan field value
            resourceManField.SetValue(null, resourceManSubstitute);
        }
    }
#endif

    /// <summary>
    /// Class that overrides default ResourceManager behavior by pseudo-localizing its content.
    /// </summary>
    class PseudoLocalizerResourceManager : ResourceManager
    {
        /// <summary>
        /// Stores a Dictionary for character translations.
        /// </summary>
        private readonly Dictionary<char, char> _translations;

        /// <summary>
        /// Initializes a new instance of the PseudoLocalizerResourceManager class.
        /// </summary>
        /// <param name="baseName">The root name of the resource file without its extension but including any fully qualified namespace name.</param>
        /// <param name="assembly">The main assembly for the resources.</param>
        public PseudoLocalizerResourceManager(string baseName, Assembly assembly)
            : base(baseName, assembly)
        {
            // Map standard "English" characters to similar-looking counterparts from other languages
            _translations = new Dictionary<char, char>
            {
                { 'a', 'ä' },
                { 'b', 'ƃ' },
                { 'c', 'č' },
                { 'd', 'ƌ' },
                { 'e', 'ë' },
                { 'f', 'ƒ' },
                { 'g', 'ğ' },
                { 'h', 'ħ' },
                { 'i', 'ï' },
                { 'j', 'ĵ' },
                { 'k', 'ƙ' },
                { 'l', 'ł' },
                { 'm', 'ɱ' },
                { 'n', 'ň' },
                { 'o', 'ö' },
                { 'p', 'þ' },
                { 'q', 'ɋ' },
                { 'r', 'ř' },
                { 's', 'š' },
                { 't', 'ŧ' },
                { 'u', 'ü' },
                { 'v', 'ṽ' },
                { 'w', 'ŵ' },
                { 'x', 'ӿ' },
                { 'y', 'ŷ' },
                { 'z', 'ž' },
                { 'A', 'Ä' },
                { 'B', 'Ɓ' },
                { 'C', 'Č' },
                { 'D', 'Đ' },
                { 'E', 'Ë' },
                { 'F', 'Ƒ' },
                { 'G', 'Ğ' },
                { 'H', 'Ħ' },
                { 'I', 'Ï' },
                { 'J', 'Ĵ' },
                { 'K', 'Ҟ' },
                { 'L', 'Ł' },
                { 'M', 'Ӎ' },
                { 'N', 'Ň' },
                { 'O', 'Ö' },
                { 'P', 'Ҏ' },
                { 'Q', 'Ǫ' },
                { 'R', 'Ř' },
                { 'S', 'Š' },
                { 'T', 'Ŧ' },
                { 'U', 'Ü' },
                { 'V', 'Ṽ' },
                { 'W', 'Ŵ' },
                { 'X', 'Ӿ' },
                { 'Y', 'Ŷ' },
                { 'Z', 'Ž' },
            };
        }

        /// <summary>
        /// Returns the value of the specified String resource.
        /// </summary>
        /// <param name="name">The name of the resource to get.</param>
        /// <returns>The value of the resource localized for the caller's current culture settings.</returns>
        public override string GetString(string name)
        {
            return PseudoLocalizeString(base.GetString(name));
        }

        /// <summary>
        /// Gets the value of the String resource localized for the specified culture.
        /// </summary>
        /// <param name="name">The name of the resource to get.</param>
        /// <param name="culture">The CultureInfo object that represents the culture for which the resource is localized.</param>
        /// <returns>The value of the resource localized for the specified culture.</returns>
        public override string GetString(string name, CultureInfo culture)
        {
            return PseudoLocalizeString(base.GetString(name, culture));
        }

        /// <summary>
        /// Pseudo-localizes a string.
        /// </summary>
        /// <param name="str">Input string.</param>
        /// <returns>Pseudo-localized string.</returns>
        private string PseudoLocalizeString(string str)
        {
            // Create a new string with the "translated" values of each character
            var translatedChars = new char[str.Length];
            for (var i = 0; i < str.Length; i++)
            {
                var c = str[i];
                translatedChars[i] = _translations.ContainsKey(c) ? _translations[c] : c;
            }

            // Return the "translated" string with some beginning/end padding
            return string.Concat("[===", new string(translatedChars), "===]");
        }

        /// <summary>
        /// Returns the value of the specified Object resource.
        /// </summary>
        /// <param name="name">The name of the resource to get.</param>
        /// <returns>The value of the resource localized for the caller's current culture settings.</returns>
        public override object GetObject(string name)
        {
            return PseudoLocalizeObject(base.GetObject(name));
        }

        /// <summary>
        /// Gets the value of the Object resource localized for the specified culture.
        /// </summary>
        /// <param name="name">The name of the resource to get.</param>
        /// <param name="culture">The CultureInfo object that represents the culture for which the resource is localized.</param>
        /// <returns>The value of the resource localized for the specified culture.</returns>
        public override object GetObject(string name, CultureInfo culture)
        {
            return PseudoLocalizeObject(base.GetObject(name, culture));
        }

        /// <summary>
        /// Pseudo-localizes an object.
        /// </summary>
        /// <param name="obj">Input object.</param>
        /// <returns>Pseudo-localized object.</returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Catch-all handler fails gracefully.")]
        private static object PseudoLocalizeObject(object obj)
        {
#if !SILVERLIGHT
            // "Translate" bitmaps by inverting every pixel
            var bitmap = obj as Bitmap;
            if (null != bitmap)
            {
                for (var y = 0; y < bitmap.Height; y++)
                {
                    for (var x = 0; x < bitmap.Width; x++)
                    {
                        var color = bitmap.GetPixel(x, y);
                        var inverseColor = Color.FromArgb(color.A, (byte)~color.R, (byte)~color.G, (byte)~color.B);
                        bitmap.SetPixel(x, y, inverseColor);
                    }
                }
            }
#endif

            // "Translate" encoded images by decoding, inverting every pixel, and re-encoding
            var byteArray = obj as byte[];
            if (null != byteArray)
            {
                try
                {
                    using (var stream = new MemoryStream(byteArray))
                    {
                        var bitmapImage = new BitmapImage();
#if SILVERLIGHT
                        bitmapImage.SetSource(stream);
#else
                        bitmapImage.BeginInit();
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.StreamSource = stream;
                        bitmapImage.EndInit();
#endif
                        var writeableBitmap = new WriteableBitmap(bitmapImage);
                        var width = writeableBitmap.PixelWidth;
                        var height = writeableBitmap.PixelHeight;
#if SILVERLIGHT
                        var pixels = writeableBitmap.Pixels;
#else
                        var pixels = new int[width * height];
                        var stride = width * 4;
                        writeableBitmap.CopyPixels(pixels, stride, 0);
#endif
                        for (var i = 0; i < pixels.Length; i++)
                        {
                            uint pixel = (uint)pixels[i];
                            pixels[i] = (int)((~pixel & 0x00ffffff) | (pixel & 0xff000000));
                        }
#if SILVERLIGHT
                        obj = PngEncoder.Encode(width, height, pixels).ToArray();
#else
                        writeableBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
                        var pngBitmapEncoder = new PngBitmapEncoder();
                        pngBitmapEncoder.Frames.Add(BitmapFrame.Create(writeableBitmap));
                        using (var memoryStream = new MemoryStream())
                        {
                            pngBitmapEncoder.Save(memoryStream);
                            obj = memoryStream.GetBuffer();
                        }
#endif
                    }
                }
                catch
                {
                    // byte[] may not have been an encoded image; leave obj alone
                }
            }

            // Return the object
            return obj;
        }
    }
}

#endif
