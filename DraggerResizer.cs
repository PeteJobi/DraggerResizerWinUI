using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.UI;
using Color = Windows.UI.Color;
using Point = Windows.Foundation.Point;
using Rect = Windows.Foundation.Rect;

namespace DraggerResizer
{
    public class DraggerResizer
    {
        private Canvas parent;
        private const int DefaultHandleThickness = 8;
        private Dictionary<FrameworkElement, Entity> entities;
        private const Orientation HorizontalAndVertical = Orientation.Horizontal | Orientation.Vertical;
        private static readonly Color Transparent = Color.FromArgb(0, 255, 255, 255);
        private Entity temporaryEntity;

        public DraggerResizer()
        {
            entities = new Dictionary<FrameworkElement, Entity>();
            temporaryEntity = new Entity{ Parameters = new HandlingParameters() };
        }

        public void InitDraggerResizer(FrameworkElement element)
        {
            InitDraggerResizer(element, default(HashSet<Orientation>));
        }

        public void InitDraggerResizer(FrameworkElement element, HashSet<Orientation>? orientations, HandlingParameters? parameters = null, HandlingCallbacks? callbacks = null)
        {
            var oriDictionary = orientations?.ToDictionary(o => o, _ => new Appearance());
            if (oriDictionary != null && oriDictionary.ContainsKey(Orientation.Horizontal) && oriDictionary.ContainsKey(Orientation.Vertical))
            {
                oriDictionary.Remove(Orientation.Horizontal);
                oriDictionary.Remove(Orientation.Vertical);
                oriDictionary.Add(HorizontalAndVertical, new Appearance());
            }
            InitDraggerResizer(element, oriDictionary, parameters, callbacks);
        }
        public void InitDraggerResizer(FrameworkElement element, Dictionary<Orientation, Appearance>? orientations, HandlingParameters? parameters = null, HandlingCallbacks? callbacks = null)
        {
            if (entities.ContainsKey(element)) throw new NotSupportedException("This element has already been added");
            var elementParent = element.Parent;
            if (elementParent is not Canvas canvas)
                throw new NotSupportedException("Resizer only works with elements contained in Canvas");
            if (parent == null) parent = canvas;
            else if (parent != canvas) throw new NotSupportedException(
                "If you initialize multiple elements, they should all have the same parent");

            orientations = ProcessAppearance(orientations);
            parameters = ProcessParameters(parameters);
            var dragOrientation = orientations.TryGetValue(HorizontalAndVertical, out var dragAppearance) ? HorizontalAndVertical :
                orientations.TryGetValue(Orientation.Horizontal, out dragAppearance) ? Orientation.Horizontal :
                orientations.TryGetValue(Orientation.Vertical, out dragAppearance) ? Orientation.Vertical : (Orientation?)null;
            var elementContainer = dragAppearance != null ? new Handle(dragAppearance.CursorShape.Value) : new Handle();
            parent.Children.Add(elementContainer);
            elementContainer.Width = double.IsNaN(element.Width) ? element.ActualWidth : element.Width;
            elementContainer.Height = double.IsNaN(element.Height) ? element.ActualHeight : element.Height;
            if(double.IsNaN(parent.Width)) parent.Width = parent.ActualWidth;
            if(double.IsNaN(parent.Height)) parent.Height = parent.ActualHeight;
            Canvas.SetTop(elementContainer, Canvas.GetTop(element));
            Canvas.SetLeft(elementContainer, Canvas.GetLeft(element));
            Canvas.SetTop(element, 0);
            Canvas.SetLeft(element, 0);
            parent.Children.Remove(element);
            elementContainer.Children.Add(element);
            var handles = new Handle?[8];
            var entity = new Entity
            {
                Parent = elementContainer,
                Handles = handles,
                Parameters = parameters,
                ZIndex = entities.Count + 1
            };
            if (dragOrientation != null)
            {
                if (dragAppearance != null)
                {
                    elementContainer.Background = dragAppearance.AtRest;
                    SetUpStates(elementContainer, dragAppearance);
                    if(!parameters.DontChangeZIndex.Value) SetupZIndexUpdates(elementContainer, element);
                }
                elementContainer.ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY;
                if (callbacks?.DragStarted != null) elementContainer.ManipulationStarted += (_, _) => callbacks.DragStarted();
                elementContainer.ManipulationDelta += (sender, args) =>
                {
                    args.Handled = true;
                    var translation = callbacks?.BeforeDragging?.Invoke(args.Delta.Translation) ?? args.Delta.Translation;
                    var finalTranslation = DragManipulationDelta(elementContainer, entity, translation, dragOrientation.Value);
                    callbacks?.AfterDragging?.Invoke(finalTranslation);
                };
                if (callbacks?.DragCompleted != null) elementContainer.ManipulationCompleted += (_, _) => callbacks.DragCompleted();
            }

            var halfHandleCount = handles.Length / 2;
            var allOrientations = Enum.GetValues<Orientation>();
            for (var i = 0; i < halfHandleCount; i++)
            {
                var pos = allOrientations[i];
                if (!orientations.TryGetValue(pos, out var appearance)) continue;
                var halfThickness = appearance.HandleThickness.Value / 2;
                var handle = handles[i] = new Handle(appearance.CursorShape.Value);
                handle.Background = appearance.AtRest;
                SetUpStates(handle, appearance);
                if (!parameters.DontChangeZIndex.Value) SetupZIndexUpdates(handle, element);
                parent.Children.Add(handle);
                if (i % 2 == 0)
                {
                    handle.Width = elementContainer.Width;
                    handle.Height = appearance.HandleThickness.Value;
                    Canvas.SetLeft(handle, Canvas.GetLeft(elementContainer));
                    Canvas.SetTop(handle, Canvas.GetTop(elementContainer) + (i == 0 ? -halfThickness : elementContainer.Height - halfThickness));
                    handle.ManipulationMode = ManipulationModes.TranslateY;
                }
                else
                {
                    handle.Height = elementContainer.Height;
                    handle.Width = appearance.HandleThickness.Value;
                    Canvas.SetTop(handle, Canvas.GetTop(elementContainer));
                    Canvas.SetLeft(handle, Canvas.GetLeft(elementContainer) + (i == 3 ? -halfThickness : elementContainer.Width - halfThickness));
                    handle.ManipulationMode = ManipulationModes.TranslateX;
                }

                if (callbacks?.ResizeStarted != null) handle.ManipulationStarted += (_, _) => callbacks.ResizeStarted.Invoke(pos);
                handle.ManipulationDelta += (sender, args) =>
                {
                    args.Handled = true;
                    var translation = callbacks?.BeforeResizing?.Invoke(args.Delta.Translation, pos) ?? args.Delta.Translation;
                    var finalTranslation = ResizeManipulationDelta(element, entity, translation, pos);
                    callbacks?.AfterResizing?.Invoke(finalTranslation, pos);
                };
                if (callbacks?.ResizeCompleted != null) handle.ManipulationCompleted += (_, _) => callbacks.ResizeCompleted.Invoke(pos);
            }

            for (var i = halfHandleCount; i < handles.Length; i++)
            {
                var pos = allOrientations[i];
                if (!orientations.TryGetValue(pos, out var appearance)) continue;
                var halfThickness = appearance.HandleThickness.Value / 2;
                var handle = handles[i] = new Handle(appearance.CursorShape.Value);
                handle.Background = appearance.AtRest;
                SetUpStates(handle, appearance);
                if (!parameters.DontChangeZIndex.Value) SetupZIndexUpdates(handle, element);
                parent.Children.Add(handle);
                handle.Width = handle.Height = appearance.HandleThickness.Value;
                handle.ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY;
                if (i % 2 == 0)
                {
                    Canvas.SetLeft(handle, Canvas.GetLeft(elementContainer) - halfThickness);
                    Canvas.SetTop(handle, Canvas.GetTop(elementContainer) + (i == halfHandleCount ? -halfThickness : elementContainer.Height - halfThickness));
                }
                else
                {
                    Canvas.SetLeft(handle, Canvas.GetLeft(elementContainer) + elementContainer.Width - halfThickness);
                    Canvas.SetTop(handle, Canvas.GetTop(elementContainer) + (i == 5 ? -halfThickness : elementContainer.Height - halfThickness));
                }

                if (callbacks?.ResizeStarted != null) handle.ManipulationStarted += (_, _) => callbacks.ResizeStarted.Invoke(pos);
                handle.ManipulationDelta += (sender, args) =>
                {
                    args.Handled = true;
                    var translation = callbacks?.BeforeResizing?.Invoke(args.Delta.Translation, pos) ?? args.Delta.Translation;
                    var finalTranslation = ResizeManipulationDelta(element, entity, translation, pos);
                    callbacks?.AfterResizing?.Invoke(finalTranslation, pos);
                };
                if (callbacks?.ResizeCompleted != null) handle.ManipulationCompleted += (_, _) => callbacks.ResizeCompleted.Invoke(pos);
            }

            entities.Add(element, entity);
        }

        public void DeInitDraggerResizer(FrameworkElement element)
        {
            if (!entities.TryGetValue(element, out var entity)) return;
            entity.Parent.Children.Remove(element);
            parent.Children.Add(element);
            Canvas.SetTop(element, Canvas.GetTop(entity.Parent));
            Canvas.SetLeft(element, Canvas.GetLeft(entity.Parent));
            parent.Children.Remove(entity.Parent);
            foreach (var handle in entity.Handles)
            {
                parent.Children.Remove(handle);
            }
            entities.Remove(element);
        }

        private void RemoveEntity(Entity entity)
        {
            parent.Children.Remove(entity.Parent);
            foreach (var handle in entity.Handles)
            {
                parent.Children.Remove(handle);
            }
        }
        public void RemoveElement(FrameworkElement element)
        {
            if (!entities.TryGetValue(element, out var entity)) return;
            RemoveEntity(entity);
            entities.Remove(element);
        }
        public void RemoveAllElements()
        {
            foreach (var entity in entities.Values)
            {
                RemoveEntity(entity);
            }
            entities.Clear();
        }

        public void DragElementHorizontally(FrameworkElement element, double translation) => DragElement(element, translation, 0);
        public void DragElementVertically(FrameworkElement element, double translation) => DragElement(element, 0, translation);
        public void DragElement(FrameworkElement element, double translationX, double translationY)
        {
            if (!entities.TryGetValue(element, out var entity)) return;
            var orientation = (translationX == 0 ? 0 : Orientation.Horizontal) |
                              (translationY == 0 ? 0 : Orientation.Vertical);
            DragManipulationDelta(entity.Parent, entity, new Point(translationX, translationY), orientation);
        }

        public void PositionElementLeft(FrameworkElement element, double left, HandlingParameters? parameters = null)
        {
            if (!entities.TryGetValue(element, out var entity)) return;
            var currentLeft = Canvas.GetLeft(entity.Parent);
            temporaryEntity.Handles = entity.Handles;
            SetTempParameters(parameters, entity.Parameters);
            DragManipulationDelta(entity.Parent, temporaryEntity, new Point(left - currentLeft, 0), HorizontalAndVertical);
        }
        public void PositionElementTop(FrameworkElement element, double top, HandlingParameters? parameters = null)
        {
            if (!entities.TryGetValue(element, out var entity)) return;
            var currentTop = Canvas.GetTop(entity.Parent);
            temporaryEntity.Handles = entity.Handles;
            SetTempParameters(parameters, entity.Parameters);
            DragManipulationDelta(entity.Parent, temporaryEntity, new Point(0, top - currentTop), HorizontalAndVertical);
        }
        public void PositionElement(FrameworkElement element, double left, double top, HandlingParameters? parameters = null)
        {
            if (!entities.TryGetValue(element, out var entity)) return;
            var currentTop = Canvas.GetTop(entity.Parent);
            var currentLeft = Canvas.GetLeft(entity.Parent);
            temporaryEntity.Handles = entity.Handles;
            SetTempParameters(parameters, entity.Parameters);
            DragManipulationDelta(entity.Parent, temporaryEntity, new Point(left - currentLeft, top - currentTop), HorizontalAndVertical);
        }

        public void PositionElementAtCenter(FrameworkElement element)
        {
            if (!entities.TryGetValue(element, out var entity)) return;
            var newLeft = (parent.Width - entity.Parent.Width) / 2;
            var newTop = (parent.Height - entity.Parent.Height) / 2;
            PositionElement(element, newLeft, newTop);
        }

        public void ResizeElementWidth(FrameworkElement element, double width, Orientation orientation = Orientation.Right, HandlingParameters? parameters = null)
        {
            if (!entities.TryGetValue(element, out var entity)) return;
            ResizeElement(element, width, entity.Parent.Height, orientation, parameters);
        }

        public void ResizeElementHeight(FrameworkElement element, double height, Orientation orientation = Orientation.Bottom, HandlingParameters? parameters = null)
        {
            if (!entities.TryGetValue(element, out var entity)) return;
            ResizeElement(element, entity.Parent.Width, height, orientation, parameters);
        }

        public void ResizeElement(FrameworkElement element, double width, double height, Orientation orientation = Orientation.BottomRight, HandlingParameters? parameters = null)
        {
            if (!entities.TryGetValue(element, out var entity)) return;
            temporaryEntity.Parent = entity.Parent;
            temporaryEntity.Handles = entity.Handles;
            SetTempParameters(parameters, entity.Parameters);
            var (deltaX, deltaY) = (width - entity.Parent.Width, height - entity.Parent.Height);
            if(orientation is Orientation.TopLeft or Orientation.Top or Orientation.TopRight) deltaY *= -1;
            if(orientation is Orientation.TopLeft or Orientation.Left or Orientation.BottomLeft) deltaX *= -1;
            ResizeManipulationDelta(element, temporaryEntity, new Point(deltaX, deltaY), orientation);
        }

        public int GetElementZIndex(FrameworkElement element)
        {
            return !entities.TryGetValue(element, out var entity) 
                ? throw new ArgumentException("Element does not exist") 
                : Canvas.GetZIndex(entity.Parent);
        }

        public void SetElementZIndex(FrameworkElement element, int zIndex)
        {
            if (!entities.TryGetValue(element, out var entity)) return;
            entity.ZIndex = zIndex;
            Canvas.SetZIndex(entity.Parent, zIndex);
            foreach (var entityHandle in entity.Handles)
            {
                if(entityHandle != null) Canvas.SetZIndex(entityHandle, zIndex);
            }
        }

        public void SetElementZIndexTopmost(FrameworkElement element)
        {
            if (!entities.TryGetValue(element, out var elementEntity)) return;
            var elementZIndex = elementEntity.ZIndex;
            if (elementZIndex >= entities.Count) return;
            var allElements = entities.Keys.Where(k => !entities[k].Parameters.DontChangeZIndex.Value);
            foreach (var key in allElements)
            {
                var entity = entities[key];
                if (key == element)
                {
                    Canvas.SetZIndex(entity.Parent, entity.ZIndex = entities.Count);
                    foreach (var entityHandle in entity.Handles)
                    {
                        if (entityHandle != null) Canvas.SetZIndex(entityHandle, entity.ZIndex);
                    }
                }
                else if (entities[key].ZIndex > elementZIndex)
                {
                    Canvas.SetZIndex(entity.Parent, --entity.ZIndex);
                    foreach (var entityHandle in entity.Handles)
                    {
                        if (entityHandle != null) Canvas.SetZIndex(entityHandle, entity.ZIndex);
                    }
                }
            }
        }

        public void SetAspectRatio(FrameworkElement element, double aspectRatio)
        {
            if (!entities.TryGetValue(element, out var entity)) return;
            var width = entity.Parent.Width;
            var height = entity.Parent.Height;
            if (width / height > aspectRatio)
            {
                width = height * aspectRatio;
            }
            else
            {
                height = width / aspectRatio;
            }
            ResizeElement(element, width, height, Orientation.BottomRight, new HandlingParameters{ KeepAspectRatio = false });
        }

        public void SetNewHandlingParameters(FrameworkElement element, HandlingParameters parameters)
        {
            if (!entities.TryGetValue(element, out var entity)) return;
            entity.Parameters = ProcessParameters(parameters);
        }

        public double GetElementLeft(FrameworkElement element)
        {
            if (entities.TryGetValue(element, out var entity)) return Canvas.GetLeft(entity.Parent);
            throw new ArgumentException("Element does not exist");
        }
        public double GetElementTop(FrameworkElement element)
        {
            if (entities.TryGetValue(element, out var entity)) return Canvas.GetTop(entity.Parent);
            throw new ArgumentException("Element does not exist");
        }

        private Dictionary<Orientation, Appearance> ProcessAppearance(Dictionary<Orientation, Appearance>? appearance)
        {
            var orientations = Enum.GetValues<Orientation>().Append(HorizontalAndVertical).ToArray();
            if (appearance == null)
            {
                appearance = orientations
                    .Where(o => o is not Orientation.Horizontal and not Orientation.Vertical)
                    .ToDictionary(o => o, o => new Appearance())!;
            }
            foreach (var ori in orientations)
            {
                if (appearance.TryGetValue(ori, out var appear))
                {
                    if (appear == null)
                    {
                        appear = new Appearance();
                        appearance[ori] = appear;
                    }
                    appear.HandleThickness ??= DefaultHandleThickness;
                    appear.CursorShape ??= ori switch
                    {
                        Orientation.Top or Orientation.Bottom or Orientation.Vertical => InputSystemCursorShape.SizeNorthSouth,
                        Orientation.Left or Orientation.Right or Orientation.Horizontal => InputSystemCursorShape.SizeWestEast,
                        Orientation.TopLeft or Orientation.BottomRight => InputSystemCursorShape.SizeNorthwestSoutheast,
                        Orientation.TopRight or Orientation.BottomLeft => InputSystemCursorShape.SizeNortheastSouthwest,
                        Orientation.Vertical | Orientation.Horizontal => InputSystemCursorShape.SizeAll,
                        _ => throw new ArgumentOutOfRangeException(nameof(ori), ori, null)
                    };
                    if (ori != HorizontalAndVertical)
                    {
                        appear.AtRest ??= new SolidColorBrush(Transparent);
                        appear.Hover ??= appear.AtRest;
                        appear.Pressed ??= appear.Hover;
                    }
                }
            }
            return appearance!;
        }

        private HandlingParameters ProcessParameters(HandlingParameters? parameters)
        {
            parameters ??= new HandlingParameters();
            parameters.Boundary ??= Boundary.BoundedAtEdges;
            parameters.DontChangeZIndex ??= false;
            parameters.KeepAspectRatio ??= false;
            parameters.MinimumWidth ??= 0;
            parameters.MinimumHeight ??= 0;
            parameters.MaximumWidth ??= double.PositiveInfinity;
            parameters.MaximumHeight ??= double.PositiveInfinity;
            parameters.BoundaryLeft ??= double.NegativeInfinity;
            parameters.BoundaryTop ??= double.NegativeInfinity;
            parameters.BoundaryRight ??= double.PositiveInfinity;
            parameters.BoundaryBottom ??= double.PositiveInfinity;

            return parameters;
        }

        private void SetUpStates(Handle handle, Appearance appearance)
        {
            handle.PointerEntered += (sender, args) =>
            {
                if (handle.State != Handle.ActiveState.Pressed)
                {
                    handle.State = Handle.ActiveState.Hovered;
                    handle.Background = appearance.Hover;
                }
                handle.HoveredAfterPointerReleased = true;
                args.Handled = true;
            };
            handle.PointerExited += (sender, args) =>
            {
                if (handle.State != Handle.ActiveState.Pressed)
                {
                    handle.State = Handle.ActiveState.AtRest;
                    handle.Background = appearance.AtRest;
                }
                handle.HoveredAfterPointerReleased = false;
                args.Handled = true;
            };
            handle.ManipulationStarted += (sender, args) =>
            {
                handle.State = Handle.ActiveState.Pressed;
                handle.Background = appearance.Pressed;
                args.Handled = true;
            };
            handle.ManipulationCompleted += (sender, args) =>
            {
                handle.State = handle.HoveredAfterPointerReleased ? Handle.ActiveState.Hovered : Handle.ActiveState.AtRest;
                handle.Background = handle.HoveredAfterPointerReleased ? appearance.Hover : appearance.AtRest;
                args.Handled = true;
            };
        }

        private void SetupZIndexUpdates(Handle handle, FrameworkElement element)
        {
            Canvas.SetZIndex(handle, entities.Count + 1);
            handle.PointerPressed += (sender, args) =>
            {
                SetElementZIndexTopmost(element);
                args.Handled = true;
            };
        }

        private void SetTempParameters(HandlingParameters? tempParameters, HandlingParameters entityParameters)
        {
            temporaryEntity.Parameters.DontChangeZIndex = tempParameters?.DontChangeZIndex ?? entityParameters.DontChangeZIndex;
            temporaryEntity.Parameters.Boundary = tempParameters?.Boundary ?? entityParameters.Boundary;
            temporaryEntity.Parameters.KeepAspectRatio = tempParameters?.KeepAspectRatio ?? entityParameters.KeepAspectRatio;
            temporaryEntity.Parameters.MinimumWidth = tempParameters?.MinimumWidth ?? entityParameters.MinimumWidth;
            temporaryEntity.Parameters.MinimumHeight = tempParameters?.MinimumHeight ?? entityParameters.MinimumHeight;
            temporaryEntity.Parameters.MaximumWidth = tempParameters?.MaximumWidth ?? entityParameters.MaximumWidth;
            temporaryEntity.Parameters.MaximumHeight = tempParameters?.MaximumHeight ?? entityParameters.MaximumHeight;
        }

        private Rect ResizeManipulationDelta(FrameworkElement element, Entity entity, Point translation, Orientation handleOrientation)
        {
            const int top = 0;
            const int right = 1;
            const int bottom = 2;
            const int left = 3;
            const int topLeft = 4;
            const int topRight = 5;
            const int bottomLeft = 6;
            const int bottomRight = 7;

            var translationX = translation.X;
            var translationY = translation.Y;
            double delta;
            var temp = 0d;
            bool outOfBoundsAndDimenBottom, outOfBoundsAndDimenRight, outOfBoundsAndDimenTop, outOfBoundsAndDimenLeft;
            var canvasLeft = Canvas.GetLeft(entity.Parent);
            var canvasTop = Canvas.GetTop(entity.Parent);
            var resultRect = new Rect(canvasLeft, canvasTop, entity.Parent.Width, entity.Parent.Height);

            switch (handleOrientation)
            {
                case Orientation.Top:
                    if (ExceededDimensionClamps(ref translationY, entity.Parent, handleOrientation, entity.Parameters) ||
                        OutOfBounds(ref translationY, entity.Parent, handleOrientation, canvasLeft, canvasTop, entity.Parameters) ||
                        OutOfBoundsAndClampsEdges(ref translationY, Orientation.Left, Orientation.Right, false, out delta)) break;
                    ProcessTop(translationY);
                    if (entity.Parameters.KeepAspectRatio.Value)
                    {
                        ProcessLeft(delta);
                        ProcessRight(-delta);
                    }
                    break;
                case Orientation.Right:
                    if (ExceededDimensionClamps(ref translationX, entity.Parent, handleOrientation, entity.Parameters) ||
                        OutOfBounds(ref translationX, entity.Parent, handleOrientation, canvasLeft, canvasTop, entity.Parameters) ||
                        OutOfBoundsAndClampsEdges(ref translationX, Orientation.Bottom, Orientation.Top, true, out delta)) break;
                    ProcessRight(translationX);
                    if (entity.Parameters.KeepAspectRatio.Value)
                    {
                        ProcessTop(-delta);
                        ProcessBottom(delta);
                    }
                    break;
                case Orientation.Bottom:
                    if (ExceededDimensionClamps(ref translationY, entity.Parent, handleOrientation, entity.Parameters) ||
                        OutOfBounds(ref translationY, entity.Parent, handleOrientation, canvasLeft, canvasTop, entity.Parameters) ||
                        OutOfBoundsAndClampsEdges(ref translationY, Orientation.Right, Orientation.Left, false, out delta)) break;
                    ProcessBottom(translationY);
                    if (entity.Parameters.KeepAspectRatio.Value)
                    {
                        ProcessLeft(-delta);
                        ProcessRight(delta);
                    }
                    break;
                case Orientation.Left:
                    if (ExceededDimensionClamps(ref translationX, entity.Parent, handleOrientation, entity.Parameters) ||
                        OutOfBounds(ref translationX, entity.Parent, handleOrientation, canvasLeft, canvasTop, entity.Parameters) ||
                        OutOfBoundsAndClampsEdges(ref translationX, Orientation.Top, Orientation.Bottom, true, out delta)) break;
                    ProcessLeft(translationX);
                    if (entity.Parameters.KeepAspectRatio.Value)
                    {
                        ProcessTop(delta);
                        ProcessBottom(-delta);
                    }
                    break;
                case Orientation.TopLeft:
                    outOfBoundsAndDimenLeft = ExceededDimensionClamps(ref translationX, entity.Parent, Orientation.Left, entity.Parameters)
                        || OutOfBounds(ref translationX, entity.Parent, Orientation.Left, canvasLeft, canvasTop, entity.Parameters);
                    outOfBoundsAndDimenTop = OutOfBoundsAndClampsCorners(ref translationX, Orientation.Top, false, ref translationY);
                    if (entity.Parameters.KeepAspectRatio.Value && (outOfBoundsAndDimenLeft || outOfBoundsAndDimenTop)) break;
                    if (!outOfBoundsAndDimenTop) ProcessTop(translationY);
                    if (!outOfBoundsAndDimenLeft) ProcessLeft(translationX);
                    break;
                case Orientation.TopRight:
                    outOfBoundsAndDimenRight = ExceededDimensionClamps(ref translationX, entity.Parent, Orientation.Right, entity.Parameters)
                        || OutOfBounds(ref translationX, entity.Parent, Orientation.Right, canvasLeft, canvasTop, entity.Parameters);
                    outOfBoundsAndDimenTop = OutOfBoundsAndClampsCorners(ref translationX, Orientation.Top, true, ref translationY);
                    if (entity.Parameters.KeepAspectRatio.Value && (outOfBoundsAndDimenRight || outOfBoundsAndDimenTop)) break;
                    if (!outOfBoundsAndDimenTop) ProcessTop(translationY);
                    if (!outOfBoundsAndDimenRight) ProcessRight(translationX);
                    break;
                case Orientation.BottomLeft:
                    outOfBoundsAndDimenLeft = ExceededDimensionClamps(ref translationX, entity.Parent, Orientation.Left, entity.Parameters)
                        || OutOfBounds(ref translationX, entity.Parent, Orientation.Left, canvasLeft, canvasTop, entity.Parameters);
                    outOfBoundsAndDimenBottom = OutOfBoundsAndClampsCorners(ref translationX, Orientation.Bottom, true, ref translationY);
                    if (entity.Parameters.KeepAspectRatio.Value && (outOfBoundsAndDimenLeft || outOfBoundsAndDimenBottom)) break;
                    if (!outOfBoundsAndDimenBottom) ProcessBottom(translationY);
                    if (!outOfBoundsAndDimenLeft) ProcessLeft(translationX);
                    break;
                case Orientation.BottomRight:
                    outOfBoundsAndDimenRight = ExceededDimensionClamps(ref translationX, entity.Parent, Orientation.Right, entity.Parameters) 
                        || OutOfBounds(ref translationX, entity.Parent, Orientation.Right, canvasLeft, canvasTop, entity.Parameters);
                    outOfBoundsAndDimenBottom = OutOfBoundsAndClampsCorners(ref translationX, Orientation.Bottom, false, ref translationY);
                    if(entity.Parameters.KeepAspectRatio.Value && (outOfBoundsAndDimenRight || outOfBoundsAndDimenBottom)) break;
                    if (!outOfBoundsAndDimenBottom) ProcessBottom(translationY);
                    if (!outOfBoundsAndDimenRight) ProcessRight(translationX);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(handleOrientation), handleOrientation, null);
            }
            Debug.WriteLine($".....{resultRect.Width}....{resultRect.Height}");
            return resultRect;

            void ProcessTop(double value)
            {
                AddHeight(-value, element, entity.Parent, entity.Handles[left], entity.Handles[right]);
                AddTop(value, entity.Parent, entity.Handles[top], entity.Handles[topLeft], entity.Handles[topRight], entity.Handles[left], entity.Handles[right]);
                resultRect.Y += value;
                resultRect.Height -= value;
            }

            void ProcessRight(double value)
            {
                AddWidth(value, element, entity.Parent, entity.Handles[top], entity.Handles[bottom]);
                AddLeft(value, entity.Handles[right], entity.Handles[topRight], entity.Handles[bottomRight]);
                resultRect.Width += value;
            }

            void ProcessBottom(double value)
            {
                AddHeight(value, element, entity.Parent, entity.Handles[left], entity.Handles[right]);
                AddTop(value, entity.Handles[bottom], entity.Handles[bottomLeft], entity.Handles[bottomRight]);
                resultRect.Height += value;
            }

            void ProcessLeft(double value)
            {
                AddWidth(-value, element, entity.Parent, entity.Handles[top], entity.Handles[bottom]);
                AddLeft(value, entity.Parent, entity.Handles[left], entity.Handles[topLeft], entity.Handles[bottomLeft], entity.Handles[top], entity.Handles[bottom]);
                resultRect.X += value;
                resultRect.Width -= value;
            }

            bool OutOfBoundsAndClampsEdges(ref double trans, Orientation deltaOrient, Orientation deltaMinusOrient, bool isVerticalAxis, out double delta)
            {
                if (!entity.Parameters.KeepAspectRatio.Value)
                {
                    delta = 0;
                    return false;
                }

                var ar = isVerticalAxis ? element.Height / element.Width : element.Width / element.Height;
                delta = trans * ar / 2;
                Debug.WriteLine($"x: {trans}, d: {delta}, l: {canvasLeft}, t: {canvasTop}, ar: {ar}");
                var savedDelta = delta;
                if (ExceededDimensionClamps(ref delta, entity.Parent, deltaOrient, entity.Parameters)) return true;
                if (OutOfBounds(ref delta, entity.Parent, deltaOrient, canvasLeft, canvasTop, entity.Parameters)) return true;
                var deltaMinus = -delta;
                if (ExceededDimensionClamps(ref deltaMinus, entity.Parent, deltaMinusOrient, entity.Parameters)) return true;
                if (OutOfBounds(ref deltaMinus, entity.Parent, deltaMinusOrient, canvasLeft, canvasTop, entity.Parameters)) return true;
                delta = -deltaMinus;
                if (delta < savedDelta) trans = delta * 2 / ar;
                Debug.WriteLine($"    x: {trans}, d: {delta}, d2: {delta*2}");
                return false;
            }

            bool OutOfBoundsAndClampsCorners(ref double transX, Orientation transYOrient, bool isNorthEast, ref double transY)
            {
                double ar = 0;
                Debug.WriteLine($"x: {transX}, y: {transY}, l: {canvasLeft}, t: {canvasTop}, ar: {ar}");
                if (entity.Parameters.KeepAspectRatio.Value)
                {
                    ar = element.Height / element.Width;
                    transY = transX * ar;
                    if(isNorthEast) transY *= -1;
                }
                var savedTransY = transY;
                if (ExceededDimensionClamps(ref transY, entity.Parent, transYOrient, entity.Parameters)) return true;
                if (OutOfBounds(ref transY, entity.Parent, transYOrient, canvasLeft, canvasTop, entity.Parameters)) return true;
                if (entity.Parameters.KeepAspectRatio.Value && transY < savedTransY)
                {
                    transX = transY / ar;
                    if(isNorthEast) transX *= -1;
                }
                Debug.WriteLine($"    x: {transX}, y: {transY}");
                return false;
            }
        }

        private Rect DragManipulationDelta(FrameworkElement element, Entity entity, Point translation, Orientation handleOrientation)
        {
            var translationX = translation.X;
            var translationY = translation.Y;
            var canvasLeft = Canvas.GetLeft(element);
            var canvasTop = Canvas.GetTop(element);
            var resultRect = new Rect(canvasLeft, canvasTop, element.Width, element.Height);
            var canMoveHorizontally = (handleOrientation & Orientation.Horizontal) == Orientation.Horizontal && !OutOfBounds(ref translationX, element, Orientation.Horizontal, canvasLeft, 0, entity.Parameters);
            var canMoveVertically = (handleOrientation & Orientation.Vertical) == Orientation.Vertical && !OutOfBounds(ref translationY, element, Orientation.Vertical, 0, canvasTop, entity.Parameters);
            Debug.WriteLine($"ch: {canMoveHorizontally}");
            if (!canMoveHorizontally && !canMoveVertically) return resultRect;

            var allElements = entity.Handles.Prepend(element).ToArray();
            if (canMoveHorizontally)
            {
                AddLeft(translationX, allElements);
                resultRect.X += translationX;
            }
            if (canMoveVertically)
            {
                AddTop(translationY, allElements);
                resultRect.Y += translationY;
            }
            Debug.WriteLine(resultRect);
            return resultRect;
        }

        private (double left, double top, double right, double bottom) GetManipulationBoundaries(FrameworkElement element, HandlingParameters parameters){
            if (parameters.Boundary == Boundary.Custom)
            {
                return (
                    parameters.BoundaryLeft.Value, 
                    parameters.BoundaryTop.Value, 
                    parameters.BoundaryRight.Value,
                    parameters.BoundaryBottom.Value
                );
            }
            var halfWidth = parameters.Boundary == Boundary.BoundedAtCenter ? element.Width / 2 : 0;
            var halfHeight = parameters.Boundary == Boundary.BoundedAtCenter ? element.Height / 2 : 0;
            var left = -halfWidth;
            var top = -halfHeight;
            var right = parent.Width + halfWidth;
            var bottom = parent.Height + halfHeight;
            return (left, top, right, bottom);
        }

        private void AddHeight(double height, params FrameworkElement?[] elements)
        {
            foreach (var frameworkElement in elements)
            {
                if (frameworkElement != null) frameworkElement.Height += height;
            }
        }

        private void AddWidth(double width, params FrameworkElement?[] elements)
        {
            foreach (var frameworkElement in elements)
            {
                if (frameworkElement != null) frameworkElement.Width += width;
            }
        }

        private void AddTop(double top, params FrameworkElement?[] elements)
        {
            foreach (var frameworkElement in elements)
            {
                if (frameworkElement != null) Canvas.SetTop(frameworkElement, Canvas.GetTop(frameworkElement) + top);
            }
        }

        private void AddLeft(double left, params FrameworkElement?[] elements)
        {
            foreach (var frameworkElement in elements)
            {
                if (frameworkElement != null) Canvas.SetLeft(frameworkElement, Canvas.GetLeft(frameworkElement) + left);
            }
        }

        private bool OutOfBounds(ref double translation, FrameworkElement element, Orientation handleOrientation, double left, double top, HandlingParameters parameters)
        {
            if (parameters.Boundary == Boundary.NoBounds) return false;
            var (leftBoundary, topBoundary, rightBoundary, bottomBoundary) = GetManipulationBoundaries(element, parameters);
            var noSpaceTop = top <= topBoundary && translation <= 0;
            var noSpaceLeft = left <= leftBoundary && translation <= 0;
            var maximumRightTranslation = rightBoundary - (left + element.Width);
            var maximumBottomTranslation = bottomBoundary - (top + element.Height);
            var noSpaceBottom = maximumBottomTranslation < 0.1 && translation >= 0;
            var noSpaceRight = maximumRightTranslation < 0.1 && translation >= 0;
            switch (handleOrientation)
            {
                case Orientation.Horizontal:
                    if (noSpaceLeft || noSpaceRight) return true;
                    translation = Math.Clamp(translation, leftBoundary - left, maximumRightTranslation);
                    break;
                case Orientation.Vertical:
                    if (noSpaceTop || noSpaceBottom) return true;
                    translation = Math.Clamp(translation, topBoundary - top, maximumBottomTranslation);
                    break;
                case Orientation.Left:
                    if (noSpaceLeft) return true;
                    translation = Math.Clamp(translation, leftBoundary - left, element.Width);
                    break;
                case Orientation.Right:
                    if (noSpaceRight) return true;
                    translation = Math.Clamp(translation, -element.Width, maximumRightTranslation);
                    break;
                case Orientation.Top:
                    if (noSpaceTop) return true;
                    translation = Math.Clamp(translation, topBoundary - top, element.Height);
                    break;
                case Orientation.Bottom:
                    if (noSpaceBottom) return true;
                    translation = Math.Clamp(translation, -element.Height, maximumBottomTranslation);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(handleOrientation), handleOrientation, null);
            }

            return false;
        }

        private bool ExceededDimensionClamps(ref double translation, FrameworkElement element, Orientation handleOrientation, HandlingParameters parameters)
        {
            switch (handleOrientation)
            {
                case Orientation.Left:
                    if ((element.Width >= parameters.MaximumWidth.Value && translation <= 0) || (element.Width <= parameters.MinimumWidth.Value && translation >= 0)) return true;
                    translation = Math.Clamp(translation, element.Width - parameters.MaximumWidth.Value, element.Width - parameters.MinimumWidth.Value);
                    break;
                case Orientation.Top:
                    if((element.Height >= parameters.MaximumHeight.Value && translation <= 0) || (element.Height <= parameters.MinimumHeight.Value && translation >= 0)) return true;
                    translation = Math.Clamp(translation, element.Height - parameters.MaximumHeight.Value, element.Height - parameters.MinimumHeight.Value);
                    break;
                case Orientation.Right:
                    if ((element.Width <= parameters.MinimumWidth.Value && translation <= 0) || (element.Width >= parameters.MaximumWidth.Value && translation >= 0)) return true;
                    translation = Math.Clamp(translation, parameters.MinimumWidth.Value - element.Width, parameters.MaximumWidth.Value - element.Width);
                    break;
                case Orientation.Bottom:
                    if ((element.Height <= parameters.MinimumHeight.Value && translation <= 0) || (element.Height >= parameters.MaximumHeight.Value && translation >= 0)) return true;
                    translation = Math.Clamp(translation, parameters.MinimumHeight.Value - element.Height, parameters.MaximumHeight.Value - element.Height);
                    break;
            }

            return false;
        }
    }

    [Flags]
    public enum Orientation
    {
        //Resize orientation
        Top = 1,
        Right = 2,
        Bottom = 4,
        Left = 8,
        TopLeft = 16,
        TopRight = 32,
        BottomLeft = 64,
        BottomRight = 128,

        //Drag orientation
        Horizontal = 256,
        Vertical = 512
    }

    public enum Boundary{ NoBounds, BoundedAtEdges, BoundedAtCenter, Custom }

    public class Handle : Canvas
    {
        public Handle()
        {
        }

        public Handle(InputSystemCursorShape cursorShape)
        {
            ProtectedCursor = InputSystemCursor.Create(cursorShape);
        }

        public bool HoveredAfterPointerReleased { get; set; }

        public ActiveState State { get; set; }

        public enum ActiveState { AtRest, Hovered, Pressed }
    }

    internal class Entity
    {
        public Handle Parent { get; set; }
        public Handle?[] Handles { get; set; }
        public HandlingParameters Parameters { get; set; }
        public int ZIndex { get; set; }
    }

    public class Appearance
    {
        public double? HandleThickness { get; set; }
        public InputSystemCursorShape? CursorShape { get; set; }
        public SolidColorBrush? AtRest { get; set; }
        public SolidColorBrush? Hover { get; set; }
        public SolidColorBrush? Pressed { get; set; }
    }

    public class HandlingParameters
    {
        public bool? KeepAspectRatio { get; set; }
        public bool? DontChangeZIndex { get; set; }
        public Boundary? Boundary { get; set; }
        public double? MinimumWidth { get; set; }
        public double? MinimumHeight { get; set; }
        public double? MaximumWidth { get; set; }
        public double? MaximumHeight { get; set; }
        public double? BoundaryLeft { get; set; }
        public double? BoundaryTop { get; set; }
        public double? BoundaryRight { get; set; }
        public double? BoundaryBottom { get; set; }
    }

    public class HandlingCallbacks
    {
        public Action? DragStarted { get; set; }
        public Func<Point, Point>? BeforeDragging { get; set; }
        public Action<Rect>? AfterDragging { get; set; }
        public Action? DragCompleted { get; set; }
        public Action<Orientation>? ResizeStarted { get; set; }
        public Func<Point, Orientation, Point>? BeforeResizing { get; set; }
        public Action<Rect, Orientation>? AfterResizing { get; set; }
        public Action<Orientation>? ResizeCompleted { get; set; }
    }
}
