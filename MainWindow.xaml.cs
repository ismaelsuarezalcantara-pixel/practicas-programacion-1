using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PuntoDeVentaFarmacia.Data;
using PuntoDeVentaFarmacia.Models;
using PuntoDeVentaFarmacia.Views;

namespace PuntoDeVentaFarmacia
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<Producto> _productos = new();
        private readonly ObservableCollection<ItemCarrito> _carrito = new();
        private const decimal TasaImpuesto = 0.18m; // ITBIS

        public MainWindow()
        {
            InitializeComponent();
            ListaProductos.ItemsSource = _productos;
            ListaCarrito.ItemsSource = _carrito;
            _carrito.CollectionChanged += (_, _) => ActualizarTotales();

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            CargarCategorias();
            CargarProductos();
            ActualizarResumenVentasHoy();
            ActualizarTotales();
        }

        // ================= CARGA DE DATOS =================

        private void CargarCategorias()
        {
            var categoriaSeleccionada = ListaCategorias.SelectedItem as string;
            var categorias = DatabaseHelper.ObtenerCategorias();
            ListaCategorias.ItemsSource = categorias;
            ListaCategorias.SelectedItem = categoriaSeleccionada != null && categorias.Contains(categoriaSeleccionada)
                ? categoriaSeleccionada
                : categorias.FirstOrDefault();
        }

        private void CargarProductos()
        {
            var categoria = ListaCategorias.SelectedItem as string;
            var busqueda = TxtBuscar.Text;

            var productos = DatabaseHelper.ObtenerProductos(categoria, busqueda);
            _productos.Clear();
            foreach (var p in productos) _productos.Add(p);

            TxtTituloCategoria.Text = string.IsNullOrEmpty(categoria) || categoria == "Todas"
                ? "Todos los productos"
                : categoria;
            TxtCantidadProductos.Text = $"{_productos.Count} producto{(_productos.Count == 1 ? "" : "s")} disponible{(_productos.Count == 1 ? "" : "s")}";
        }

        private void ActualizarResumenVentasHoy()
        {
            var (cantidad, total) = DatabaseHelper.ObtenerResumenVentasHoy();
            TxtVentasHoyTotal.Text = $"RD$ {total:N2}";
            TxtVentasHoyCantidad.Text = $"{cantidad} transaccion{(cantidad == 1 ? "" : "es")}";
        }

        // ================= FILTROS =================

        private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e)
        {
            TxtPlaceholderBuscar.Visibility = string.IsNullOrEmpty(TxtBuscar.Text) ? Visibility.Visible : Visibility.Collapsed;
            CargarProductos();
        }

        private void ListaCategorias_SelectionChanged(object sender, SelectionChangedEventArgs e) => CargarProductos();

        // ================= CARRITO =================

        private void BtnAgregarAlCarrito_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not Producto producto) return;
            if (producto.SinStock) return;

            var itemExistente = _carrito.FirstOrDefault(i => i.ProductoId == producto.Id);
            if (itemExistente != null)
            {
                if (itemExistente.Cantidad >= producto.Stock)
                {
                    MessageBox.Show("No hay más existencias disponibles de este producto.", "Stock insuficiente",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                itemExistente.Cantidad++;
            }
            else
            {
                _carrito.Add(new ItemCarrito
                {
                    ProductoId = producto.Id,
                    Nombre = producto.Nombre,
                    Categoria = producto.Categoria,
                    PrecioUnitario = producto.Precio,
                    StockDisponible = producto.Stock,
                    RequiereReceta = producto.RequiereReceta,
                    Cantidad = 1,
                });
            }
            ActualizarTotales();
        }

        private void BtnSumarCantidad_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not ItemCarrito item) return;
            if (item.Cantidad >= item.StockDisponible)
            {
                MessageBox.Show("No hay más existencias disponibles de este producto.", "Stock insuficiente",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            item.Cantidad++;
            ActualizarTotales();
        }

        private void BtnRestarCantidad_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not ItemCarrito item) return;
            item.Cantidad--;
            if (item.Cantidad <= 0) _carrito.Remove(item);
            ActualizarTotales();
        }

        private void BtnQuitarDelCarrito_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not ItemCarrito item) return;
            _carrito.Remove(item);
            ActualizarTotales();
        }

        private void BtnVaciarCarrito_Click(object sender, RoutedEventArgs e)
        {
            if (_carrito.Count == 0) return;
            var confirmar = MessageBox.Show("¿Vaciar todos los productos del carrito?", "Confirmar",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmar == MessageBoxResult.Yes)
            {
                _carrito.Clear();
                ActualizarTotales();
            }
        }

        private void ActualizarTotales()
        {
            var subtotal = _carrito.Sum(i => i.Subtotal);
            var impuesto = subtotal * TasaImpuesto;
            var total = subtotal + impuesto;

            TxtSubtotal.Text = $"RD$ {subtotal:N2}";
            TxtImpuesto.Text = $"RD$ {impuesto:N2}";
            TxtTotal.Text = $"RD$ {total:N2}";
            TxtCantidadCarrito.Text = _carrito.Sum(i => i.Cantidad).ToString();

            TxtCarritoVacio.Visibility = _carrito.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            BtnCobrar.IsEnabled = _carrito.Count > 0;
        }

        // ================= COBRO =================

        private void BtnCobrar_Click(object sender, RoutedEventArgs e)
        {
            if (_carrito.Count == 0) return;

            var subtotal = _carrito.Sum(i => i.Subtotal);
            var impuesto = subtotal * TasaImpuesto;
            var total = subtotal + impuesto;

            var checkout = new CheckoutDialog(total) { Owner = this };
            if (checkout.ShowDialog() != true) return;

            var ventaId = DatabaseHelper.RegistrarVenta(_carrito, checkout.MetodoPago, total, checkout.MontoRecibido, checkout.Cambio);

            var recibo = new ReciboDialog(ventaId, _carrito.ToList(), subtotal, impuesto, total, checkout.MetodoPago, checkout.MontoRecibido, checkout.Cambio)
            {
                Owner = this
            };
            recibo.ShowDialog();

            _carrito.Clear();
            ActualizarTotales();
            CargarProductos();
            ActualizarResumenVentasHoy();
        }

        // ================= ADMINISTRACIÓN =================

        private void BtnGestionarProductos_Click(object sender, RoutedEventArgs e)
        {
            var ventana = new GestionProductosWindow { Owner = this };
            ventana.ShowDialog();
            CargarCategorias();
            CargarProductos();
        }

        // ================= CONTROLES DE VENTANA =================

        private void BtnMinimizar_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void BtnMaximizar_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Close();
    }
}
