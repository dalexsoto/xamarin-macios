//
// VTUtilities.cs
//
// Authors: 
//   Aaron Bockover <abock@xamarin.com>
//
// Copyright 2015 Xamarin Inc. All rights reserved.
//

using System;
using System.Runtime.InteropServices;

using ObjCRuntime;
using CoreGraphics;
using CoreMedia;
using CoreVideo;

#nullable enable

namespace VideoToolbox {

#if NET
	[SupportedOSPlatform ("macos")]
	[SupportedOSPlatform ("ios")]
	[SupportedOSPlatform ("tvos")]
	[SupportedOSPlatform ("maccatalyst")]
#endif
	public static class VTUtilities {
		[DllImport (Constants.VideoToolboxLibrary)]
		extern static VTStatus VTCreateCGImageFromCVPixelBuffer (
			/* CM_NONNULL CVPixelBufferRef */ IntPtr pixelBuffer,
			/* CM_NULLABLE CFDictionaryRef */ IntPtr options,
			/* CM_RETURNS_RETAINED_PARAMETER CM_NULLABLE CGImageRef * CM_NONNULL */ out IntPtr imageOut);

		// intentionally not exposing the (NSDictionary options) argument
		// since header docs indicate that there are no options available
		// as of 9.0/10.11 and to always pass NULL
		public static VTStatus ToCGImage (this CVPixelBuffer pixelBuffer, out CGImage? image)
		{
			if (pixelBuffer is null)
				ObjCRuntime.ThrowHelper.ThrowArgumentNullException (nameof (pixelBuffer));

			var ret = VTCreateCGImageFromCVPixelBuffer (pixelBuffer.GetCheckedHandle (),
				IntPtr.Zero, // no options as of 9.0/10.11 - always pass NULL
				out var imagePtr);

			image = Runtime.GetINativeObject<CGImage> (imagePtr, true); // This is already retained CM_RETURNS_RETAINED_PARAMETER

			return ret;
		}

#if MONOMAC

#if NET
		[SupportedOSPlatform ("macos11.0")]
		[UnsupportedOSPlatform ("maccatalyst")]
		[UnsupportedOSPlatform ("tvos")]
		[UnsupportedOSPlatform ("ios")]
#else
		[NoWatch]
		[NoTV]
		[NoiOS]
		[Mac (11,0)]
#endif
		[DllImport (Constants.VideoToolboxLibrary)]
		static extern void VTRegisterSupplementalVideoDecoderIfAvailable (uint codecType);

#if NET
		[SupportedOSPlatform ("macos11.0")]
		[UnsupportedOSPlatform ("maccatalyst")]
		[UnsupportedOSPlatform ("tvos")]
		[UnsupportedOSPlatform ("ios")]
#else
		[NoWatch]
		[NoTV]
		[NoiOS]
		[Mac (11,0)]
#endif
		public static void RegisterSupplementalVideoDecoder (CMVideoCodecType codecType)
			=> VTRegisterSupplementalVideoDecoderIfAvailable ((uint) codecType);
#endif
	}
}
