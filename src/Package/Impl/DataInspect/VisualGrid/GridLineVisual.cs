﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Windows;
using System.Windows.Media;

namespace Microsoft.VisualStudio.R.Package.DataInspect {
    /// <summary>
    /// DrawingVisual for grid's line
    /// </summary>
    internal class GridLineVisual : DrawingVisual {
        public GridLineVisual(VisualGrid owner) {
            Owner = owner;
        }

        internal VisualGrid Owner { get; }

        private ScrollDirection ScrollDirection {
            get {
                return Owner.ScrollDirection;
            }
        }

        public double GridLineThickness { get { return 1.0; } }

        private Brush _gridLineBrush = Brushes.Black;
        public Brush GridLineBrush {
            get {
                return _gridLineBrush;
            }
            set {
                _gridLineBrush = value;
            }
        }

        public void Draw(
            GridRange range,
            IPoints points) {

            DrawingContext drawingContext = RenderOpen();
            DoubleCollection xCollection = new DoubleCollection();
            DoubleCollection yCollection = new DoubleCollection();

            try {
                // vertical line
                double renderHeight = points.yPosition[range.Rows.Start + range.Rows.Count] - points.yPosition[range.Rows.Start];
                Rect verticalLineRect = new Rect(new Size(GridLineThickness, renderHeight));
                foreach (int i in range.Columns.GetEnumerable()) {
                    verticalLineRect.X = points.xPosition[i + 1] - GridLineThickness;
                    drawingContext.DrawRectangle(GridLineBrush, null, verticalLineRect);
                    xCollection.Add(verticalLineRect.X);
                }

                // horizontal line
                double renderWidth = points.xPosition[range.Columns.Start + range.Columns.Count] - points.xPosition[range.Columns.Start];
                Rect horizontalLineRect = new Rect(new Size(renderWidth, GridLineThickness));
                foreach (int i in range.Rows.GetEnumerable()) {
                    horizontalLineRect.Y = points.yPosition[i + 1] - GridLineThickness;
                    drawingContext.DrawRectangle(GridLineBrush, null, horizontalLineRect);
                    yCollection.Add(horizontalLineRect.Y);
                }

                XSnappingGuidelines = xCollection;
                YSnappingGuidelines = yCollection;
            } finally {
                drawingContext.Close();
            }
        }
    }
}
