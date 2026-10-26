import { useState } from "react";
import { Loader2, Plus, Edit2, Trash2, X } from "lucide-react";
import toast from "react-hot-toast";
import { useProductos, useMutacionesProducto } from "../../../hooks/useProductos";
import type { ProductoDto } from "../../../api/productosApi";

export function GestionProductos() {
  const { data: productos = [], isLoading } = useProductos();
  const { crearMutation, actualizarMutation, eliminarMutation } = useMutacionesProducto();

  const [modalOpen, setModalOpen] = useState(false);
  const [editingProducto, setEditingProducto] = useState<ProductoDto | null>(null);

  // Form state
  const [nombre, setNombre] = useState("");
  const [precioUnitario, setPrecioUnitario] = useState("");
  const [precioVenta, setPrecioVenta] = useState("");
  const [stockInicial, setStockInicial] = useState("0");
  const [marca, setMarca] = useState("");
  const [sabor, setSabor] = useState("");
  const [calorias, setCalorias] = useState("");
  const [proteinas, setProteinas] = useState("");
  const [carbohidratos, setCarbohidratos] = useState("");
  const [grasas, setGrasas] = useState("");

  const handleOpenNuevo = () => {
    setEditingProducto(null);
    setNombre("");
    setPrecioUnitario("");
    setPrecioVenta("");
    setStockInicial("0");
    setMarca("");
    setSabor("");
    setCalorias("");
    setProteinas("");
    setCarbohidratos("");
    setGrasas("");
    setModalOpen(true);
  };

  const handleOpenEditar = (p: ProductoDto) => {
    setEditingProducto(p);
    setNombre(p.nombre);
    setPrecioUnitario(p.precioUnitario.toString());
    setPrecioVenta(p.precioVenta.toString());
    setStockInicial(p.stockActual.toString()); // Note: only useful for display/readonly in edit
    setMarca(p.marca || "");
    setSabor(p.sabor || "");
    setCalorias(p.calorias?.toString() || "");
    setProteinas(p.proteinas?.toString() || "");
    setCarbohidratos(p.carbohidratos?.toString() || "");
    setGrasas(p.grasas?.toString() || "");
    setModalOpen(true);
  };

  const handleDelete = (id: number) => {
    if (confirm("¿Estás seguro de eliminar este producto?")) {
      toast.promise(eliminarMutation.mutateAsync(id), {
        loading: "Eliminando...",
        success: "Producto eliminado",
        error: "Error al eliminar producto",
      });
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!nombre.trim() || precioUnitario === "" || precioVenta === "") {
      toast.error("Completá los campos obligatorios");
      return;
    }
    if (!(Number(precioUnitario) >= 0) || !(Number(precioVenta) >= 0)) {
      toast.error("Los precios deben ser números de 0 o más");
      return;
    }
    if (!editingProducto && stockInicial !== "" && !(Number.isInteger(Number(stockInicial)) && Number(stockInicial) >= 0)) {
      toast.error("El stock inicial debe ser un número entero de 0 o más");
      return;
    }
    const nutricionales = [calorias, proteinas, carbohidratos, grasas];
    if (nutricionales.some(v => v !== "" && !(Number(v) >= 0))) {
      toast.error("Los valores nutricionales no pueden ser negativos");
      return;
    }

    const baseData = {
      nombre,
      precioUnitario: parseFloat(precioUnitario),
      precioVenta: parseFloat(precioVenta),
      marca: marca || undefined,
      sabor: sabor || undefined,
      calorias: calorias ? parseFloat(calorias) : undefined,
      proteinas: proteinas ? parseFloat(proteinas) : undefined,
      carbohidratos: carbohidratos ? parseFloat(carbohidratos) : undefined,
      grasas: grasas ? parseFloat(grasas) : undefined,
    };

    try {
      if (editingProducto) {
        await actualizarMutation.mutateAsync({
          id: editingProducto.id,
          ...baseData
        });
        toast.success("Producto actualizado");
      } else {
        await crearMutation.mutateAsync({
          ...baseData,
          stockInicial: parseInt(stockInicial || "0")
        });
        toast.success("Producto creado");
      }
      setModalOpen(false);
    } catch (err: any) {
      toast.error(err?.message || "Ocurrió un error al guardar el producto");
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <header className="flex items-center justify-between">
        <div>
          <h2 className="text-lg font-semibold text-slate-900">Gestión de Productos</h2>
          <p className="text-sm text-slate-500">Administrá el catálogo y ABM de productos.</p>
        </div>
        <button
          onClick={handleOpenNuevo}
          className="flex items-center gap-2 rounded-md bg-emerald-600 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-700"
        >
          <Plus className="h-4 w-4" />
          Nuevo Producto
        </button>
      </header>

      {/* Grid List */}
      <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white">
        {isLoading ? (
          <div className="flex h-32 items-center justify-center">
            <Loader2 className="h-6 w-6 animate-spin text-slate-400" />
          </div>
        ) : (
          <table className="w-full text-left text-sm">
            <thead className="border-b border-slate-200 bg-slate-50 text-xs uppercase text-slate-500">
              <tr>
                <th className="px-4 py-3">Nombre</th>
                <th className="px-4 py-3">Marca/Sabor</th>
                <th className="px-4 py-3 text-right">Costo</th>
                <th className="px-4 py-3 text-right">Venta</th>
                <th className="px-4 py-3 text-right">Stock</th>
                <th className="px-4 py-3 text-center">Acciones</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200">
              {productos.map((p) => (
                <tr key={p.id} className="hover:bg-slate-50">
                  <td className="px-4 py-3 font-medium text-slate-900">{p.nombre}</td>
                  <td className="px-4 py-3 text-slate-500 text-xs">
                    {p.marca && <span className="font-semibold">{p.marca}</span>}
                    {p.sabor && <span> - {p.sabor}</span>}
                  </td>
                  <td className="px-4 py-3 text-right text-slate-600">${p.precioUnitario}</td>
                  <td className="px-4 py-3 text-right font-medium text-slate-900">${p.precioVenta}</td>
                  <td className="px-4 py-3 text-right font-bold text-emerald-600">{p.stockActual}</td>
                  <td className="px-4 py-3">
                    <div className="flex justify-center gap-2">
                      <button onClick={() => handleOpenEditar(p)} className="text-slate-400 hover:text-blue-600">
                        <Edit2 className="h-4 w-4" />
                      </button>
                      <button onClick={() => handleDelete(p.id)} className="text-slate-400 hover:text-rose-600">
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
              {productos.length === 0 && (
                <tr>
                  <td colSpan={6} className="py-8 text-center text-slate-500">No hay productos.</td>
                </tr>
              )}
            </tbody>
          </table>
        )}
      </div>

      {/* Modal Form */}
      {modalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm p-4">
          <div className="w-full max-w-2xl max-h-[90vh] overflow-y-auto rounded-xl bg-white shadow-xl">
            <div className="flex items-center justify-between border-b border-slate-100 p-4">
              <h3 className="font-semibold text-slate-900">
                {editingProducto ? "Editar Producto" : "Nuevo Producto"}
              </h3>
              <button onClick={() => setModalOpen(false)} className="text-slate-400 hover:text-slate-600">
                <X className="h-5 w-5" />
              </button>
            </div>

            <form onSubmit={handleSubmit} className="p-4 space-y-6">
              {/* Sección Básica */}
              <div>
                <h4 className="text-sm font-semibold text-slate-900 border-b pb-2 mb-3">Datos Básicos</h4>
                <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                  <div className="sm:col-span-2">
                    <label className="mb-1 block text-xs font-medium text-slate-700">Nombre *</label>
                    <input type="text" required value={nombre} onChange={e => setNombre(e.target.value)} className="w-full rounded border p-2 text-sm" />
                  </div>
                  <div>
                    <label className="mb-1 block text-xs font-medium text-slate-700">Precio Unitario (Costo) *</label>
                    <input type="number" step="0.01" min="0" required value={precioUnitario} onChange={e => setPrecioUnitario(e.target.value)} className="w-full rounded border p-2 text-sm" />
                  </div>
                  <div>
                    <label className="mb-1 block text-xs font-medium text-slate-700">Precio Venta *</label>
                    <input type="number" step="0.01" min="0" required value={precioVenta} onChange={e => setPrecioVenta(e.target.value)} className="w-full rounded border p-2 text-sm" />
                  </div>
                  {!editingProducto && (
                    <div>
                      <label className="mb-1 block text-xs font-medium text-slate-700">Stock Inicial</label>
                      <input type="number" min="0" step="1" value={stockInicial} onChange={e => setStockInicial(e.target.value)} className="w-full rounded border p-2 text-sm" />
                    </div>
                  )}
                </div>
              </div>

              {/* Sección Nutricional */}
              <div>
                <h4 className="text-sm font-semibold text-slate-900 border-b pb-2 mb-3">Info. Nutricional / Detalles (Opcional)</h4>
                <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                  <div>
                    <label className="mb-1 block text-xs font-medium text-slate-700">Marca</label>
                    <input type="text" value={marca} onChange={e => setMarca(e.target.value)} className="w-full rounded border p-2 text-sm" />
                  </div>
                  <div>
                    <label className="mb-1 block text-xs font-medium text-slate-700">Sabor</label>
                    <input type="text" value={sabor} onChange={e => setSabor(e.target.value)} className="w-full rounded border p-2 text-sm" />
                  </div>
                  <div>
                    <label className="mb-1 block text-xs font-medium text-slate-700">Calorías (kcal)</label>
                    <input type="number" step="0.01" value={calorias} onChange={e => setCalorias(e.target.value)} className="w-full rounded border p-2 text-sm" />
                  </div>
                  <div>
                    <label className="mb-1 block text-xs font-medium text-slate-700">Proteínas (g)</label>
                    <input type="number" step="0.01" value={proteinas} onChange={e => setProteinas(e.target.value)} className="w-full rounded border p-2 text-sm" />
                  </div>
                  <div>
                    <label className="mb-1 block text-xs font-medium text-slate-700">Carbohidratos (g)</label>
                    <input type="number" step="0.01" value={carbohidratos} onChange={e => setCarbohidratos(e.target.value)} className="w-full rounded border p-2 text-sm" />
                  </div>
                  <div>
                    <label className="mb-1 block text-xs font-medium text-slate-700">Grasas (g)</label>
                    <input type="number" step="0.01" value={grasas} onChange={e => setGrasas(e.target.value)} className="w-full rounded border p-2 text-sm" />
                  </div>
                </div>
              </div>

              <div className="flex justify-end gap-3 pt-4">
                <button type="button" onClick={() => setModalOpen(false)} className="rounded px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100">
                  Cancelar
                </button>
                <button type="submit" disabled={crearMutation.isPending || actualizarMutation.isPending} className="rounded bg-emerald-600 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50">
                  Guardar
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
