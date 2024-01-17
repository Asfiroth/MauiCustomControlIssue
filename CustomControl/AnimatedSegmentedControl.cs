using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Maui.Controls.Shapes;

namespace CustomControl;

[ContentProperty("Items")]
public class AnimatedSegmentedControl : Grid
{
    private const int AnimationDuration = 250;

    private int _sectionWidth;
    private readonly Border _itemBackgroundBorder;
    private readonly HorizontalStackLayout _itemStackLayout;
    
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource),
        typeof(IList<AnimatedSegmentedControlItem>),
        typeof(AnimatedSegmentedControl),
        propertyChanged: OnItemsSourcePropertyChanged);
    
    public IList<AnimatedSegmentedControlItem> ItemsSource
    {
        get => (IList<AnimatedSegmentedControlItem>)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }
    
    private static void OnItemsSourcePropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not AnimatedSegmentedControl animatedSegmentedControl) return;
        if (newValue is not IList<AnimatedSegmentedControlItem> itemsSource) return;
        animatedSegmentedControl.SetItemsSource(itemsSource);
    }
    
    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items),
        typeof(ObservableCollection<AnimatedSegmentedControlItem>),
        typeof(AnimatedSegmentedControl),
        propertyChanged: OnItemsPropertyChanged);
    
    public ObservableCollection<AnimatedSegmentedControlItem> Items
    {
        get => (ObservableCollection<AnimatedSegmentedControlItem>)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }
    
    private static void OnItemsPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not AnimatedSegmentedControl animatedSegmentedControl) return;
        if (newValue is not ObservableCollection<AnimatedSegmentedControlItem> items) return;
        animatedSegmentedControl.SetItems(items);
    }
    
    public AnimatedSegmentedControl()
    {
        Items = new ObservableCollection<AnimatedSegmentedControlItem>();
        
        BackgroundColor = Colors.Transparent;

        ColumnSpacing = 0;
        RowSpacing = 10;
        ColumnDefinitions = new ColumnDefinitionCollection
        {
            new()
        };
        
        RowDefinitions = new RowDefinitionCollection
        {
            new(){ Height = 41 },
            new(){ Height = GridLength.Star }
        };
        
        _itemBackgroundBorder = new Border
        {
            Background = GetThemedColor("#FFFFFF", "#FFFFFF"),
            StrokeShape = new RoundRectangle { CornerRadius = 11 },
            Margin = new Thickness(6,3,6,3),
            Shadow = new Shadow
            {
                Brush = Color.FromArgb("#9EA1A5"),
                Opacity = 0.4f,
                Offset = new Point(0, 0),
            },
            HorizontalOptions = LayoutOptions.Start
        };

        _itemStackLayout = new HorizontalStackLayout
        {
            Spacing = 0,
            Background = Colors.Transparent,
            Margin = new Thickness(6,3,6,3),
        };

        var backgroundBorder = new Border
        {
            Background = GetThemedColor("#F3F3F3", "#EA6D1F"),
            StrokeShape = new RoundRectangle { CornerRadius = 11 },
            Margin = new Thickness(28, 0, 28, 0),
            Content = new Grid
            {
                Children =
                {
                    _itemBackgroundBorder,
                    _itemStackLayout
                }
            }
        };
        
        backgroundBorder.PropertyChanged += BackgroundBorderOnPropertyChanged;
        
        this.Add(backgroundBorder, 0);
    }

    private Color GetThemedColor(string light, string dark)
    {
        if (Application.Current is not null && Application.Current.PlatformAppTheme == AppTheme.Light)
            return Color.FromArgb(light);
        
        return Color.FromArgb(dark);
    }

    private void BackgroundBorderOnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is not Border backgroundBorder) return;
        
        if (args.PropertyName != nameof(backgroundBorder.Width)) return;
        
        _sectionWidth = (int)(backgroundBorder.Width / _itemStackLayout.Children.Count) - 6;

#if ANDROID
        Dispatcher.Dispatch(() =>
        {
            _itemBackgroundBorder.WidthRequest = _sectionWidth;

            foreach (var view in _itemStackLayout.Children)
            {
                if(view is Label label)
                    label.WidthRequest = _sectionWidth;
            }
        });
#elif IOS
        _itemBackgroundBorder.WidthRequest = _sectionWidth;

        foreach (var view in _itemStackLayout.Children)
        {
            if(view is Label label)
                label.WidthRequest = _sectionWidth;
        }
#endif
    }

    private void ItemsOnCollectionChanged(object? _, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is null) return;
        
        foreach (AnimatedSegmentedControlItem item in e.NewItems)
        {
            if (!_itemStackLayout.Children.Any())
            {
                item.IsSelected = true;
                this.Add(item.Content, 0, 1);
            }
            else
                item.Content.IsVisible = false;
                
            var label = new Label
            {
                Text = item.Text,
                FontSize = 13,
                FontFamily = "Roboto",
                TextColor = item.IsSelected ? GetThemedColor("#2E3137", "#2E3137") : GetThemedColor("#5A5F69", "#FFFFFF"),
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                FontAttributes = item.IsSelected ? FontAttributes.Bold : FontAttributes.None
            };

            //add tap gesture to label
            var tapGestureRecognizer = new TapGestureRecognizer();

            tapGestureRecognizer.Tapped += tapGestureRecognizerOnTapped;

            label.GestureRecognizers.Add(tapGestureRecognizer);

            _itemStackLayout.Children.Add(label);
            
            this.Add(item.Content, 0, 1);
        }
    }

    private void tapGestureRecognizerOnTapped(object? sender, TappedEventArgs args)
    {
        if (sender is not Label label) return;
        
        var index = _itemStackLayout.Children.IndexOf(label);
        
        var item = Items[index];
        
        if (item.IsSelected) return;
        
        var selectedItem = Items.FirstOrDefault(x => x.IsSelected);
        
        if (selectedItem is null) throw new NullReferenceException(nameof(selectedItem));
        
        var selectedItemIndex = Items.IndexOf(selectedItem);
        
        var contentTranslationOutFactor = index > selectedItemIndex ? -1 : 1;
        var contentTranslationInFactor = index > selectedItemIndex ? 1 : -1;
        
        item.Content.TranslationX = item.Content.Width * contentTranslationInFactor;

        item.Content.IsVisible = true;
        
        selectedItem.IsSelected = false;
            
        selectedItem.Content.Animate("Move", new Animation
        {
            {
                0, 1,
                new Animation(v => selectedItem.Content.TranslationX = v,
                    selectedItem.Content.TranslationX, selectedItem.Content.Width * contentTranslationOutFactor, Easing.Linear,
                    () =>
                    {
                        selectedItem.Content.IsVisible = false;
                    })
            }
        }, length: AnimationDuration);
        
        item.IsSelected = true;
        
        var newPosition = index * _sectionWidth;
        
        foreach (var view in _itemStackLayout.Children) 
        {
            if (view is not Label lbl) return;
            lbl.FontAttributes = FontAttributes.None;
            lbl.TextColor = GetThemedColor("#5A5F69", "#FFFFFF");
        }

        label.FontAttributes = FontAttributes.Bold;
        label.TextColor = GetThemedColor("#2E3137", "#2E3137");
        
        _itemBackgroundBorder.Animate("Move", new Animation
        {
            {
                0, 1,
                new Animation(v => _itemBackgroundBorder.TranslationX = v,
                    _itemBackgroundBorder.TranslationX, newPosition, Easing.Linear)
            }
        }, length: AnimationDuration);

        item.Content.Animate("Move", new Animation
        {
            {
                0, 1,
                new Animation(v => item.Content.TranslationX = v,
                    item.Content.TranslationX, 0, Easing.Linear)
            }
        }, length: AnimationDuration);
    }

    private void SetItems(ObservableCollection<AnimatedSegmentedControlItem>? items)
    {
        if (items is null) return;
        
        Items.CollectionChanged -= ItemsOnCollectionChanged;
        Items.CollectionChanged += ItemsOnCollectionChanged;

        if(_itemStackLayout?.Children is null) return;  
        if(_itemStackLayout.Children.Any()) _itemStackLayout.Children.Clear();
    }
    
    private void SetItemsSource(IList<AnimatedSegmentedControlItem>? itemsSource)
    {
        if (itemsSource is null) return;
        foreach (var item in itemsSource)
        {
            Items.Add(item);
        }
    }
}

public class AnimatedSegmentedControlItem : ContentView
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(AnimatedSegmentedControlItem),
        string.Empty);
    
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
    
    public static readonly BindableProperty IsSelectedProperty = BindableProperty.Create(
        nameof(IsSelected),
        typeof(bool),
        typeof(AnimatedSegmentedControlItem),
        false);
    
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }
    
    public AnimatedSegmentedControlItem()
    {
        BackgroundColor = Colors.Transparent;
    }
}