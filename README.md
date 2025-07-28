# DraggerResizerWinUI
This is a single-class WinUI 3 C# library that allows you to reposition and resize elements in a canvas.

![DRTest 2025-07-28 14-54-15000_CROPPED](https://github.com/user-attachments/assets/1d23bff4-f1a8-4e71-a0b3-0a88b223720a)

# How to use
Include this Shared Library into your WinUI solution, and reference it in your WinUI project. The below shows the minimum code required to use this library:
XAML
```
<Canvas Height="500" Width="500" Background="Yellow" Loaded="FrameworkElement_OnLoaded">
    <Rectangle Name="Rect" Height="100" Width="100" Stroke="Black" StrokeThickness="3" Canvas.Top="200" Canvas.Left="200" Fill="Transparent"/>
</Canvas>
```

Code-Behind
```
private void FrameworkElement_OnLoaded(object sender, RoutedEventArgs e)
{
    new DraggerResizer.DraggerResizer().InitDraggerResizer(Rect);
}
```
Two things you need to note from the onset
1. The target element must be a direct child of a canvas element, and this canvas must be loaded before you initialize the target.
2. The target element must have a background if you want it to be dragged via interaction. If it has to be transparent, set the background (or fill) to a transparent brush.

# API Documentation
- Instance creation: Create the draggerResizer object using the parameterless contructor. The constructor doesn't do anything, so you can create the object at anytime, even before the target element has loaded. One object can be reused for multiple targets.
  ```
  DraggerResizer.DraggerResizer dr = new DraggerResizer.DraggerResizer();
  ```
  For the following APIs, we'll assume that a variable called "dr" holds a reference to the draggerResizer object.
- Initialization: To make your target draggable and resizable, you need to call one of three initialization overload methods. 
  - **InitDraggerResizer(FrameworkElement element)** <br>
    Use this to initialize a target element with default settings. The position and dimensions of the target as they were before calling the method is kept. By default, all edges and corners of the target element can be used to resize it, and the element can be dragged both orizontally and vertically (provided it has a background). By default, the element is bounded within the canvas at its edges, i.e, no part of the bounding rect of the element can extend across its parent canvas. By default, there are no visual changes when handling the element, and aspect ratio is not maintained.
    ```
    dr.InitDraggerResizer(Rect);
    ```
  - **InitDraggerResizer(FrameworkElement element, HashSet<Orientation>? orientations, HandlingParameters? parameters = null, HandlingCallbacks? callbacks = null)** <br>
    Use this to initialize a target with specific orientations and optionally, with parameters and callbacks for handling the element. Those are described in detail in the [Enums and Classes](#enums-and-classes) section.
    ```
    var orientations
    // The element will be draggable horizontally and can be resized with the left and right edges.
    var orientations = new HashSet<Orientation> { Orientation.Horizontal, Orientation.Left, Orientation.Right };
    
    // The element will maintain its aspect ratio while resizing.
    var parameters = new HandlingParameters { KeepAspectRatio = true };
    
    // We write to the console every time user lets go of the element after dragging.
    var callbacks = new HandlingCallbacks { DragCompleted = () => Console.WriteLine("User stopped dragging") };
    
    dr.InitDraggerResizer(Rect, orientations, parameters, callbacks);
    ```
  - **InitDraggerResizer(FrameworkElement element, Dictionary<Orientation, Appearance>? orientations, HandlingParameters? parameters = null, HandlingCallbacks? callbacks = null)** <br>
    This is the same as above, except that you can change some visual properties of the handles, like the background colour of the handle if it is hovered or pressed. The HashSet of Orientations is now a Dictionary of Orientations and their Appearances.
    ```
    // The element will be draggable horizontally and vertically and can be resized in four directions.
    // When any of these handles are pressed, the handle's color will change to the specified brush from the application's resources to indicate that the handle is pressed.
    var orientations = new Dictionary<Orientation, Appearance>
    {
        { Orientation.Left, new Appearance{ Pressed = (SolidColorBrush)Application.Current.Resources["LeftHandlePressed"] } },
        { Orientation.Right, new Appearance{ Pressed = (SolidColorBrush)Application.Current.Resources["RightHandlePressed"] } },
        { Orientation.Top, new Appearance{ Pressed = (SolidColorBrush)Application.Current.Resources["TopHandlePressed"] } },
        { Orientation.Bottom, new Appearance{ Pressed = (SolidColorBrush)Application.Current.Resources["BottomHandlePressed"] } },
        { Orientation.Horizontal|Orientation.Vertical, new Appearance{ Pressed = (SolidColorBrush)Application.Current.Resources["DragHandlePressed"] } },
    };
    
    var parameters = new HandlingParameters { KeepAspectRatio = true };
    var callbacks = new HandlingCallbacks { DragCompleted = () => Console.WriteLine("User stopped dragging") };
    
    dr.InitDraggerResizer(Rect, orientations, parameters, callbacks);
    ```
    
- **DeInitDraggerResizer(FrameworkElement element)** <br>
Call this on an initialized target to deinitialize it. This removes all its handles from the visual tree and references to it from the dr object. The position and dimensions of the target as they were before calling the method is kept.

- **RemoveElement(FrameworkElement element)** <br>
Call this to completely remove an element from the visual tree.

- **RemoveAllElements()** <br>
  Call this to remove all targets if your dr instance has initialized more than one.

# Enums and Classes
- **Orientation**: These represents the position of resize handles and directions allowed for dragging.
  ```
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
  ```
  Note that if you're initializing an element with the third overload method (the one that takes a dictionary of Orientation and Appearance), if you want to drag in both horizontal and vertical directions, you need to specify the orientation in one bitwise value as _Orientation.Horizontal|Orientation.Vertical_. This is not necessary for the second overload method (Hashset), but it's supported. By default, all resize and drag orientations are enabled.
- **Boundary**: This represents the way the target element's position is restricted in the canvas.
  ```
  public enum Boundary{ NoBounds, BoundedAtEdges, BoundedAtCenter, Custom }
  ```
  - **NoBounds**: If this is set, the target's position will not be restricted. The target can be dragged as far as the mouse can move on the monitor.
  - **BoundedAtEdges**: If this is set, the whole target is restricted within the canvas. The edges of the target cannot go beyond the edges of the canvas. This is the default.
  - **BoundedAtCenter**: If this is set, the target's center is restricted within the canvas. The center of the target cannot go beyond the edges of the canvas.
  - **Custom**: With this, you can define custom bounds for the target, using properties defined in the **HandlingParameters** class which are BoundaryLeft, BoundaryTop, BoundaryRight and BoundaryBottom.
- **Appearance**: This defines some properties of individual handles.
  ```
  public class Appearance
  {
      public double? HandleThickness { get; set; }
      public InputSystemCursorShape? CursorShape { get; set; }
      public SolidColorBrush? AtRest { get; set; }
      public SolidColorBrush? Hover { get; set; }
      public SolidColorBrush? Pressed { get; set; }
  }
  ```
  - **HandleThickness**: This defines the thickness of resize handles. Larger values will make it easier for users to grab the handle, but make sure it's not large enough to obstruct the drag handle. This property does not apply to drag orientations.
  - **CursorShape**: This defines the type of cursor that shows when a handle is hovered upon. You can find a list of them [here](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.input.inputsystemcursorshape?view=windows-app-sdk-1.6). The default for each orientation is shown below:
    - Top or Bottom or Vertical: SizeNorthSouth
    - Left or Right or Horizontal: SizeWestEast
    - TopLeft or BottomRight: SizeNorthwestSoutheast
    - TopRight or BottomLeft: SizeNortheastSouthwest
    - Vertical|Horizontal: SizeAll
  - **AtRest**: This defines the colour of a handle when it's not being interacted with. This applies to the drag handle, but if the target has a non-transparent background, it won't be visible. The default is Transparent.
  - **Hover**: This defines the colour of a handle when it is hovered upon. This applies to the drag handle, but if the target has a non-transparent background, it won't be visible. If it is not set, it falls back to AtRest.
  - **Pressed**: This defines the colour of a handle when it is pressed down. This applies to the drag handle, but if the target has a non-transparent background, it won't be visible. If it is not set, it falls back to Hover.
- **HandlingParameters**: This defines some behaviour of the element that pertains to handling it.
  ```
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
  ```
  - **KeepAspectRatio**: If this is set to true, then the aspect ratio (ratio of width to height) of the element is maintained. The default is false.
  - **DontChangeZIndex**: By deafult, if a draggerResizer instance has initialized multiple targets, it automatically sets the Z-Index of the currently/most-recently handled target as the highest i.e places it on top of the others. If you set this property to True, it won't change the Z-Index of the target element.
  - **Boundary**: This sets the boundary behaviour of the target. The default is BoundedAtCenter.
  - **MinimumWidth**: This defines the minimum width the target can be resized to. The default is 0.
  - **MinimumHeight**: This defines the minimum height the target can be resized to. The default is 0.
  - **MaximumWidth**: This defines the maximum width the target can be resized to. The default is infinity.
  - **MaximumHeight**: This defines the maximum height the target can be resized to. The default is infinity.
  - **BoundaryLeft**: This defines the boundary to which the element's left edge is restricted. This value is relative to the canvas, so a value of 0 means the element's left edge will not go beyond the canvas' left edge. A value of 10 means the element's left edge won't get any closer than 10 pixels away from the canvas' left edge. A value of -10 means the element's left edge can go beyond the left edge of the canvas, but no further than 10 pixels away from it.
    The default is negative infinity i.e no boundary.
  - **BoundaryTop**: This defines the boundary to which the element's top edge is restricted. This value is also relative to the canvas. The default is negative infinity.
  - **BoundaryRight**: This defines the boundary to which the element's right edge is restricted. This value is relative to the canvas, so a value of 500 means the element's right edge will not go further than 500 pixels away from the canvas' left edge. The difference between BoundaryLeft and BoundaryRight must be more than the width of the target, since it has to contain it. The default is positive infinity.
  - **BoundaryBottom**: This defines the boundary to which the element's bottom edge is restricted. This value is also relative to the canvas. The default is positive infinity.
- **HandlingCallbacks**: Use this to perform actions before, during or after the target gets dragged or resized.
  ```
  public class HandlingCallbacks
  {
      public Action? DragStarted { get; set; }
      public Action? Dragging { get; set; }
      public Action? DragCompleted { get; set; }
      public Action<Orientation>? ResizeStarted { get; set; }
      public Action<Orientation>? Resizing { get; set; }
      public Action<Orientation>? ResizeCompleted { get; set; }
  }
  ```
  - **DragStarted**: This gets called just when a user starts to drag the target.
  - **Dragging**: This gets called while the user drags for every change in the position.
  - **DragCompleted**: This gets called when the user stops dragging.
  - **ResizeStarted**: This gets called when the user starts to resize with any of the handles. The orientation of the handle is passed into the action.
  - **Resizing**: This gets called while the user resizes with any of the handles for every change in the size. The orientation of the handle is passed into the action.
  - **ResizeCompleted**: This gets called when the user stops resizing with any of the handles. The orientation of the handle is passed into the action.
