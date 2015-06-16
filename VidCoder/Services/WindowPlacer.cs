using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using VidCoder.Extensions;
using VidCoder.Model;
using VidCoder.Model.WindowPlacer;
using VidCoder.ViewModel;
using VidCoder.ViewModel.DataModels;

namespace VidCoder.Services
{
	/// <summary>
	/// Places new windows. Finds open space for them dynamically.
	/// </summary>
	public class WindowPlacer
	{
		private const double Spacing = 4;

		public Rect? PlaceWindow(Window window)
		{
			double width = window.Width;
			double height = window.Height;

			Size size = new Size(width, height);

			List<WindowPosition> openedWindowPositions = WindowManager.GetOpenedWindowPositions(excludeWindow: window);
			List<WindowPosition> savedWindowPositions = GetSavedWindowPositions();

			if (openedWindowPositions.Count == 0)
			{
				// Open at top left
				Rect workArea = SystemParameters.WorkArea;
				return new Rect(workArea.X + 15, workArea.Y + 15, width, height);
			}

			var mergedWindowPositions = new List<WindowPosition>(openedWindowPositions);
			foreach (var windowPosition in savedWindowPositions)
			{
				if (!mergedWindowPositions.Any(p => p.ViewModelType == windowPosition.ViewModelType))
				{
					mergedWindowPositions.Add(windowPosition);
				}
			}

			// Get candidate locations
			List<WindowPlacerLocation> candidates = GetCandidateLocations(mergedWindowPositions.Select(p => p.Position).ToList(), size);
			Rect mainWindowPosition = openedWindowPositions.Single(p => p.ViewModelType == typeof (MainViewModel)).Position;
			if (candidates.Count == 0)
			{
				return null;
				//return GetDefaultPosition(mainWindowPosition, size);
			}

			// Calculate how far each one is from the main window
			foreach (var windowPosition in candidates)
			{
				windowPosition.DistanceToMain = Distance(windowPosition.Position, mainWindowPosition);
			}

			// Pick the closest one to the main window
			double minDistance = candidates.Min(c => c.DistanceToMain);
			candidates = candidates.Where(c => c.DistanceToMain == minDistance).ToList();
			if (candidates.Count == 1)
			{
				return candidates[0].Position;
			}

			// Tiebreak by anchor side similarity
			foreach (var windowPosition in candidates)
			{
				double windowSideSize = windowPosition.Position.GetSide(windowPosition.AnchorSide);
				double anchorSideSize = windowPosition.AnchorPosition.GetSide(windowPosition.AnchorSide);

				if (windowSideSize > anchorSideSize)
				{
					windowPosition.AnchorSimilarityFraction = anchorSideSize / windowSideSize;
				}
				else
				{
					windowPosition.AnchorSimilarityFraction = windowSideSize / anchorSideSize;
				}
			}

			double maxSimilarity = candidates.Max(c => c.AnchorSimilarityFraction);
			candidates = candidates.Where(c => c.AnchorSimilarityFraction == maxSimilarity).ToList();

			if (candidates.Count == 1)
			{
				return candidates[0].Position;
			}

			// Prefer anchoring to the right or bottom side
			bool anyLeft = false, anyRight = false, anyTop = false, anyBottom = false;
			foreach (var windowPosition in candidates)
			{
				switch (windowPosition.AnchorSide)
				{
					case Side.Top:
						anyTop = true;
						break;
					case Side.Bottom:
						anyBottom = true;
						break;
					case Side.Left:
						anyLeft = true;
						break;
					case Side.Right:
						anyRight = true;
						break;
				}
			}

			if (anyLeft && anyRight || anyTop && anyBottom)
			{
				candidates = candidates.Where(c => c.AnchorSide == Side.Right || c.AnchorSide == Side.Bottom).ToList();

				if (candidates.Count == 1)
				{
					return candidates[0].Position;
				}
			}

			// Prefer aligning top or left
			anyLeft = anyRight = anyTop = anyBottom = false;
			foreach (var windowPosition in candidates)
			{
				switch (windowPosition.Alignment)
				{
					case Side.Top:
						anyTop = true;
						break;
					case Side.Bottom:
						anyBottom = true;
						break;
					case Side.Left:
						anyLeft = true;
						break;
					case Side.Right:
						anyRight = true;
						break;
				}
			}

			if (anyLeft && anyRight || anyTop && anyBottom)
			{
				candidates = candidates.Where(c => c.Alignment == Side.Left || c.Alignment == Side.Top).ToList();
			}

			// If there are still ties, go with the first match
			return candidates[0].Position;
		}

		private static List<WindowPlacerLocation> GetCandidateLocations(IList<Rect> existingWindows, Size size)
		{
			var candidates = new List<WindowPlacerLocation>();
			double width = size.Width;
			double height = size.Height;
			foreach (var pos in existingWindows)
			{
				Rect leftTopPosition = new Rect(pos.Left - width - Spacing, pos.Top, width, height);
				AddIfValid(new WindowPlacerLocation { AnchorSide = Side.Left, Alignment = Side.Top, Position = leftTopPosition, AnchorPosition = pos }, candidates, existingWindows);

				Rect leftBottomPosition = new Rect(pos.Left - width - Spacing, pos.Bottom - height, width, height);
				AddIfValid(new WindowPlacerLocation { AnchorSide = Side.Left, Alignment = Side.Bottom, Position = leftBottomPosition, AnchorPosition = pos }, candidates, existingWindows);

				Rect rightTopPosition = new Rect(pos.Right + Spacing, pos.Top, width, height);
				AddIfValid(new WindowPlacerLocation { AnchorSide = Side.Right, Alignment = Side.Top, Position = rightTopPosition, AnchorPosition = pos }, candidates, existingWindows);

				Rect rightBottomPosition = new Rect(pos.Left + Spacing, pos.Bottom - height, width, height);
				AddIfValid(new WindowPlacerLocation { AnchorSide = Side.Right, Alignment = Side.Bottom, Position = rightBottomPosition, AnchorPosition = pos }, candidates, existingWindows);

				Rect topLeftPosition = new Rect(pos.Left, pos.Top - height - Spacing, width, height);
				AddIfValid(new WindowPlacerLocation { AnchorSide = Side.Top, Alignment = Side.Left, Position = topLeftPosition, AnchorPosition = pos }, candidates, existingWindows);

				Rect topRightPosition = new Rect(pos.Right - width, pos.Top - height - Spacing, width, height);
				AddIfValid(new WindowPlacerLocation { AnchorSide = Side.Top, Alignment = Side.Right, Position = topRightPosition, AnchorPosition = pos }, candidates, existingWindows);

				Rect bottomLeftPosition = new Rect(pos.Left, pos.Bottom + Spacing, width, height);
				AddIfValid(new WindowPlacerLocation { AnchorSide = Side.Bottom, Alignment = Side.Left, Position = bottomLeftPosition, AnchorPosition = pos }, candidates, existingWindows);

				Rect bottomRightPosition = new Rect(pos.Right - width, pos.Bottom + Spacing, width, height);
				AddIfValid(new WindowPlacerLocation { AnchorSide = Side.Bottom, Alignment = Side.Right, Position = bottomRightPosition, AnchorPosition = pos }, candidates, existingWindows);
			}

			return candidates;
		}

		private static void AddIfValid(WindowPlacerLocation windowPlacerLocation, List<WindowPlacerLocation> list, IEnumerable<Rect> existingWindows)
		{
			Rect workArea = SystemParameters.WorkArea;
			if (!workArea.Contains(windowPlacerLocation.Position))
			{
				return;
			}

			if (Overlaps(windowPlacerLocation.Position, existingWindows))
			{
				return;
			}

			list.Add(windowPlacerLocation);
		}



		private static List<WindowPosition> GetSavedWindowPositions()
		{
			var result = new List<WindowPosition>();
			AddSavedWindowPosition<MainViewModel>(Config.MainWindowPlacement, result);
			AddSavedWindowPosition<EncodingViewModel>(Config.EncodingDialogPlacement, result);
			AddSavedWindowPosition<PreviewViewModel>(Config.PreviewWindowPlacement, result);
			AddSavedWindowPosition<LogViewModel>(Config.LogWindowPlacement, result);
			AddSavedWindowPosition<PickerViewModel>(Config.PickerWindowPlacement, result);
			return result;
		}

		private static void AddSavedWindowPosition<T>(string placementJson, List<WindowPosition> list)
		{
			if (!string.IsNullOrEmpty(placementJson))
			{
				list.Add(new WindowPosition
				{
					Position = WindowPlacement.ParsePlacementJson(placementJson).ToRect(),
					ViewModelType = typeof(T)
				});
			}
		}

		//private static Rect GetDefaultPosition(Rect mainWindowLocation, Size size)
		//{
		//	double width = size.Width;
		//	double height = size.Height;
		//	Rect workArea = SystemParameters.WorkArea;
		//	double centerX = (workArea.Left + workArea.Right) / 2;
		//	double centerY = (workArea.Top + workArea.Bottom) / 2;

		//	// Open in center of workarea
		//	Rect location = new Rect(centerX - width / 2, centerY - height / 2, width, height);
		//	return location;
		//}

		private static bool Overlaps(Rect placement, IEnumerable<Rect> openWindows)
		{
			return openWindows.Any(w => w.IntersectsWith(placement));
		}

		private double Distance(Rect a, Rect b)
		{
			if (a.IntersectsWith(b))
			{
				return 0;
			}

			bool isHorizontallyAligned = a.Right >= b.Left && b.Right >= a.Left;
			if (isHorizontallyAligned)
			{
				if (a.Top > b.Top)
				{
					// a is below b
					return a.Top - b.Bottom;
				}
				else
				{
					// b is below a
					return b.Top - a.Bottom;
				}
			}

			bool isVerticallyAligned = a.Bottom >= b.Top && b.Bottom >= a.Top;
			if (isVerticallyAligned)
			{
				if (a.Left > b.Left)
				{
					// a is to the right of b
					return a.Left - b.Right;
				}
				else
				{
					// b is to the right of a
					return b.Left - a.Right;
				}
			}

			double horizontalDistance = Math.Min(Math.Abs(a.Left - b.Right), Math.Abs(a.Right - b.Left));
			double verticalDistance = Math.Min(Math.Abs(a.Top - b.Bottom), Math.Abs(a.Bottom - b.Top));

			return horizontalDistance + verticalDistance;
		}
	}
}
