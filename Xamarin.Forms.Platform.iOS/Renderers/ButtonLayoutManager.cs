﻿using System;
using System.ComponentModel;
using System.Threading.Tasks;
using CoreGraphics;
using UIKit;

namespace Xamarin.Forms.Platform.iOS
{
	// TODO: The entire layout system. iOS buttons were not designed for
	//       anyting but image left, text right, single line layouts.

	public class ButtonLayoutManager : IDisposable
	{
		bool _disposed;
		IButtonLayoutRenderer _renderer;
		Button _element;
		bool _preserveInitialPadding;
		bool _spacingAdjustsPadding;
		bool _borderAdjustsPadding;

		UIEdgeInsets? _defaultImageInsets;
		UIEdgeInsets? _defaultTitleInsets;
		UIEdgeInsets? _defaultContentInsets;

		UIEdgeInsets _paddingAdjustments = new UIEdgeInsets();

		public ButtonLayoutManager(IButtonLayoutRenderer renderer,
			bool preserveInitialPadding = false,
			bool spacingAdjustsPadding = true,
			bool borderAdjustsPadding = false)
		{
			_renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
			_renderer.ElementChanged += OnElementChanged;
			_preserveInitialPadding = preserveInitialPadding;
			_spacingAdjustsPadding = spacingAdjustsPadding;
			_borderAdjustsPadding = borderAdjustsPadding;

			ImageElementManager.Init(renderer.ImageVisualElementRenderer);
		}

		UIButton Control => _renderer?.Control;

		IImageVisualElementRenderer ImageVisualElementRenderer => _renderer?.ImageVisualElementRenderer;

		public void Dispose()
		{
			Dispose(true);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposed)
			{
				if (disposing)
				{
					if (_renderer != null)
					{
						var imageRenderer = ImageVisualElementRenderer;
						if (imageRenderer != null)
							ImageElementManager.Dispose(imageRenderer);

						_renderer.ElementChanged -= OnElementChanged;
						_renderer = null;
					}
				}
				_disposed = true;
			}
		}

		public CGSize SizeThatFits(CGSize size, CGSize measured)
		{
			if (_disposed || _renderer == null || _element == null)
				return measured;

			var control = Control;
			if (control == null)
				return measured;

			var minHeight = _renderer.MinimumHeight;
			if (measured.Height < minHeight)
				measured.Height = minHeight;

			return measured;

			// TODO: Calculate the best size and then render the button properly

			//EnsureDefaultInsets();

			//var layout = _element.ContentLayout;
			//var padding = GetPaddingInsets();

			//// calculate the content area after removing the borders and paddings
			//var subtractedSize = new CGSize(padding.Left + padding.Right, padding.Top + padding.Bottom);
			//if (_borderAdjustsPadding && _element is IBorderElement borderElement && borderElement.IsBorderWidthSet() && borderElement.BorderWidth != borderElement.BorderWidthDefaultValue)
			//{
			//	var adjustment = (nfloat)(_element.BorderWidth * 2.0);
			//	subtractedSize += new CGSize(adjustment, adjustment);
			//}

			//// make image and spacing adjustments
			//var imageSize = control.CurrentImage?.Size ?? new CGSize();
			//if (!imageSize.IsEmpty)
			//{
			//	if (_spacingAdjustsPadding)
			//	{
			//		var adjustment = layout.Spacing / 2;
			//		subtractedSize += new CGSize(adjustment, adjustment);
			//	}

			//	subtractedSize += new CGSize(
			//		layout.IsHorizontal ? imageSize.Width + layout.Spacing : 0,
			//		layout.IsVertical ? imageSize.Height + layout.Spacing : 0);
			//}

			//var availableSize = size - subtractedSize;
			//var t = control.TitleRectForContentRect(new CGRect(CGPoint.Empty, availableSize));
			//var textSize = control.TitleRectForContentRect(new CGRect(CGPoint.Empty, availableSize)).Size;

			//var minContentSize = new CGSize(
			//	layout.IsVertical ? Math.Max(imageSize.Width, textSize.Width) : 0,
			//	layout.IsHorizontal ? Math.Max(imageSize.Height, textSize.Height) : 0);

			//var newSize = textSize + subtractedSize;
			//if (newSize.Width < minContentSize.Width)
			//	newSize.Width = minContentSize.Width;
			//if (newSize.Height < minContentSize.Height)
			//	newSize.Height = minContentSize.Height;

			//Console.WriteLine($"{_element.TabIndex}: size={size}, newSize={newSize}, subtractedSize={subtractedSize}, textSize={textSize}, minContentSize={minContentSize}");

			//return newSize;
		}

		public void Update()
		{
			UpdatePadding();
			_ = UpdateImageAsync();
			UpdateText();
			UpdateEdgeInsets();
		}

		public void SetImage(UIImage image)
		{
			if (_disposed || _renderer == null || _element == null)
				return;

			var control = Control;
			if (control == null)
				return;

			if (image != null)
			{
				control.SetImage(image.ImageWithRenderingMode(UIImageRenderingMode.AlwaysOriginal), UIControlState.Normal);
				control.ImageView.ContentMode = UIViewContentMode.ScaleAspectFit;
			}
			else
			{
				control.SetImage(null, UIControlState.Normal);
			}

			UpdateEdgeInsets();
		}

		void OnElementChanged(object sender, ElementChangedEventArgs<Button> e)
		{
			if (_element != null)
			{
				_element.PropertyChanged -= OnElementPropertyChanged;
				_element = null;
			}

			if (e.NewElement is Button button)
			{
				_element = button;
				_element.PropertyChanged += OnElementPropertyChanged;
			}

			Update();
		}

		void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (_disposed || _renderer == null || _element == null)
				return;

			if (e.PropertyName == Button.PaddingProperty.PropertyName)
				UpdatePadding();
			else if (e.PropertyName == Button.ImageProperty.PropertyName)
				_ = UpdateImageAsync();
			else if (e.PropertyName == Button.TextProperty.PropertyName)
				UpdateText();
			else if (e.PropertyName == Button.ContentLayoutProperty.PropertyName)
				UpdateEdgeInsets();
			else if (e.PropertyName == Button.BorderWidthProperty.PropertyName && _borderAdjustsPadding)
				UpdateEdgeInsets();
		}

		void UpdateText()
		{
			if (_disposed || _renderer == null || _element == null)
				return;

			var control = Control;
			if (control == null)
				return;

			control.SetTitle(_element.Text, UIControlState.Normal);

			UpdateEdgeInsets();
		}

		async Task UpdateImageAsync()
		{
			if (_disposed || _renderer == null || _element == null)
				return;

			var imageRenderer = ImageVisualElementRenderer;
			if (imageRenderer == null)
				return;

			try
			{
				await ImageElementManager.SetImage(imageRenderer, _element);
			}
			catch (Exception ex)
			{
				Internals.Log.Warning(nameof(ImageRenderer), "Error loading image: {0}", ex);
			}
		}

		void UpdatePadding()
		{
			if (_disposed || _renderer == null || _element == null)
				return;

			var control = Control;
			if (control == null)
				return;

			EnsureDefaultInsets();

			control.ContentEdgeInsets = GetPaddingInsets(_paddingAdjustments);
		}

		UIEdgeInsets GetPaddingInsets(UIEdgeInsets adjustments = default(UIEdgeInsets))
		{
			var defaultPadding = _preserveInitialPadding && _defaultContentInsets.HasValue
				? _defaultContentInsets.Value
				: new UIEdgeInsets();

			return new UIEdgeInsets(
				(nfloat)_element.Padding.Top + defaultPadding.Top + adjustments.Top,
				(nfloat)_element.Padding.Left + defaultPadding.Left + adjustments.Left,
				(nfloat)_element.Padding.Bottom + defaultPadding.Bottom + adjustments.Bottom,
				(nfloat)_element.Padding.Right + defaultPadding.Right + adjustments.Right);
		}

		void EnsureDefaultInsets()
		{
			if (_disposed || _renderer == null || _element == null)
				return;

			var control = Control;
			if (control == null)
				return;

			if (_defaultImageInsets == null)
				_defaultImageInsets = control.ImageEdgeInsets;
			if (_defaultTitleInsets == null)
				_defaultTitleInsets = control.TitleEdgeInsets;
			if (_defaultContentInsets == null)
				_defaultContentInsets = control.ContentEdgeInsets;
		}

		void UpdateEdgeInsets()
		{
			if (_disposed || _renderer == null || _element == null)
				return;

			var control = Control;
			if (control == null)
				return;

			EnsureDefaultInsets();

			_paddingAdjustments = new UIEdgeInsets();

			var imageInsets = new UIEdgeInsets();
			var titleInsets = new UIEdgeInsets();

			// adjust for the border
			if (_borderAdjustsPadding && _element is IBorderElement borderElement && borderElement.IsBorderWidthSet() && borderElement.BorderWidth != borderElement.BorderWidthDefaultValue)
			{
				var width = (nfloat)_element.BorderWidth;
				_paddingAdjustments.Top += width;
				_paddingAdjustments.Bottom += width;
				_paddingAdjustments.Left += width;
				_paddingAdjustments.Right += width;
			}

			var layout = _element.ContentLayout;

			var spacing = (nfloat)layout.Spacing;
			var halfSpacing = spacing / 2;

			var image = control.CurrentImage;
			if (image != null)
			{
				// TODO: Do not use the title label as it is not yet updated and
				//       if we move the image, then we technically have more
				//       space and will require a new laoyt pass.

				var titleRect = control.TitleLabel.Bounds.Size;

				var titleWidth = titleRect.Width;
				var titleHeight = titleRect.Height;
				var imageWidth = image.Size.Width;
				var imageHeight = image.Size.Height;

				// adjust the padding for the spacing
				if (layout.IsHorizontal)
				{
					var adjustment = _spacingAdjustsPadding ? halfSpacing * 2 : halfSpacing;
					_paddingAdjustments.Left += adjustment;
					_paddingAdjustments.Right += adjustment;
				}
				else
				{
					var adjustment = _spacingAdjustsPadding ? halfSpacing * 2 : halfSpacing;

					_paddingAdjustments.Top += adjustment;
					_paddingAdjustments.Bottom += adjustment;
				}

				// move the images according to the layout
				if (layout.Position == Button.ButtonContentLayout.ImagePosition.Left)
				{
					// add a bit of spacing
					imageInsets.Left -= halfSpacing;
					imageInsets.Right += halfSpacing;
					titleInsets.Left += halfSpacing;
					titleInsets.Right -= halfSpacing;
				}
				else if (layout.Position == Button.ButtonContentLayout.ImagePosition.Right)
				{
					// swap the elements and add spacing
					imageInsets.Left += titleWidth + halfSpacing;
					imageInsets.Right -= titleWidth + halfSpacing;
					titleInsets.Left -= imageWidth + halfSpacing;
					titleInsets.Right += imageWidth + halfSpacing;
				}
				else
				{
					// we will move the image and title vertically
					var imageVertical = (titleHeight / 2) + halfSpacing;
					var titleVertical = (imageHeight / 2) + halfSpacing;

					// the width will be different now that the image is no longer next to the text
					var horizontalAdjustment = (nfloat)(titleWidth + imageWidth - Math.Max(titleWidth, imageWidth)) / 2;
					_paddingAdjustments.Left -= horizontalAdjustment;
					_paddingAdjustments.Right -= horizontalAdjustment;

					// the height will also be different
					var verticalAdjustment = (nfloat)Math.Min(imageVertical, titleVertical);
					_paddingAdjustments.Top += verticalAdjustment;
					_paddingAdjustments.Bottom += verticalAdjustment;

					// if the image is at the bottom, swap the direction
					if (layout.Position == Button.ButtonContentLayout.ImagePosition.Bottom)
					{
						imageVertical = -imageVertical;
						titleVertical = -titleVertical;
					}

					// move the image and title vertically
					imageInsets.Top -= imageVertical;
					imageInsets.Bottom += imageVertical;
					titleInsets.Top += titleVertical;
					titleInsets.Bottom -= titleVertical;

					// center the elements horizontally
					var imageHorizontal = titleWidth / 2;
					var titleHorizontal = imageWidth / 2;
					imageInsets.Left += imageHorizontal;
					imageInsets.Right -= imageHorizontal;
					titleInsets.Left -= titleHorizontal;
					titleInsets.Right += titleHorizontal;
				}
			}

			UpdatePadding();
			control.ImageEdgeInsets = imageInsets;
			control.TitleEdgeInsets = titleInsets;
		}
	}
}
