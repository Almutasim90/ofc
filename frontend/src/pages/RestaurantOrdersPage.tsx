import { useEffect, useMemo, useState } from "react";
import { HubConnectionBuilder } from "@microsoft/signalr";
import { useTranslation } from "react-i18next";
import { api, apiEndpoint, ApiError } from "../api/client";
import type {
  BranchDto,
  BranchFeatureFlagDto,
  ComboComponentDto,
  MenuCategoryDto,
  MenuItemDto,
  ModifierGroupDto,
  OrderTypeDto,
  RestaurantOrderDto,
  RestaurantTableDto,
} from "../api/types";
import Money from "../components/Money";
import { useToast } from "../components/ToastContext";
import { useAuth } from "../auth/AuthContext";
type Draft = {
  item: MenuItemDto;
  quantity: number;
  modifierOptionIds: string[];
  comboSelections: { comboComponentId: string; selectedMenuItemId: string }[];
  delta: number;
};
export default function RestaurantOrdersPage() {
  const { t, i18n } = useTranslation();
  const { token } = useAuth();
  const toast = useToast();
  const [branches, setBranches] = useState<BranchDto[]>([]),
    [branchId, setBranchId] = useState(""),
    [types, setTypes] = useState<OrderTypeDto[]>([]),
    [typeId, setTypeId] = useState(""),
    [tables, setTables] = useState<RestaurantTableDto[]>([]),
    [tableId, setTableId] = useState(""),
    [carEnabled, setCarEnabled] = useState(false),
    [carPlate, setCarPlate] = useState(""),
    [cats, setCats] = useState<MenuCategoryDto[]>([]),
    [items, setItems] = useState<MenuItemDto[]>([]),
    [cart, setCart] = useState<Draft[]>([]),
    [config, setConfig] = useState<{
      item: MenuItemDto;
      groups: ModifierGroupDto[];
      slots: ComboComponentDto[];
      selected: string[];
    } | null>(null),
    [orders, setOrders] = useState<RestaurantOrderDto[]>([]);
  const name = (x: { nameAr: string; nameEn: string }) =>
    i18n.language === "ar" ? x.nameAr : x.nameEn;
  useEffect(() => {
    void (async () => {
      const [b, ty, it] = await Promise.all([
        api.get<BranchDto[]>("/api/branches"),
        api.get<OrderTypeDto[]>("/api/restaurant-orders/types"),
        api.get<MenuItemDto[]>("/api/restaurant-catalog/items"),
      ]);
      setBranches(b);
      setBranchId(b[0]?.id ?? "");
      setTypes(ty);
      setTypeId(ty[0]?.id ?? "");
      setItems(it);
    })();
  }, []);
  useEffect(() => {
    if (!branchId) return;
    void Promise.all([
      api.get<RestaurantTableDto[]>(
        `/api/restaurant-catalog/tables?branchId=${branchId}`,
      ),
      api.get<MenuCategoryDto[]>(
        `/api/restaurant-catalog/categories?branchId=${branchId}`,
      ),
      api.get<RestaurantOrderDto[]>(
        `/api/restaurant-orders?branchId=${branchId}`,
      ),
      api.get<BranchFeatureFlagDto[]>(
        `/api/restaurant-catalog/branches/${branchId}/features`,
      ),
    ]).then(([a, b, c, f]) => {
      setTables(a.filter((x) => x.isActive));
      setCats(b.filter((x) => x.isActive && x.isAvailable));
      setOrders(c);
      setCarEnabled(
        f.some((x) => x.featureKey === "CAR_PICKUP" && x.isEnabled),
      );
    });
  }, [branchId]);
  useEffect(() => {
    if (!branchId || !token) return;
    const connection = new HubConnectionBuilder()
      .withUrl(apiEndpoint("/hubs/restaurant-orders"), {
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect()
      .build();
    const update = (next: RestaurantOrderDto) =>
      setOrders((current) => [
        next,
        ...current.filter((order) => order.id !== next.id),
      ]);
    connection.on("QrOrderPendingApproval", update);
    connection.on("QrOrderApproved", update);
    connection.on("QrOrderRejected", update);
    connection.on("QrOrderUpdated", update);
    connection.onreconnected(() => connection.invoke("JoinBranch", branchId));
    void connection
      .start()
      .then(() => connection.invoke("JoinBranch", branchId))
      .catch(() => {});
    return () => {
      void connection.stop();
    };
  }, [branchId, token]);
  const visible = useMemo(
    () =>
      items.filter(
        (x) => x.isActive && cats.some((c) => c.id === x.categoryId),
      ),
    [items, cats],
  );
  const visibleTypes = useMemo(
    () => types.filter((x) => x.code !== "CAR_PICKUP" || carEnabled),
    [types, carEnabled],
  );
  useEffect(() => {
    if (visibleTypes.length && !visibleTypes.some((x) => x.id === typeId))
      setTypeId(visibleTypes[0].id);
  }, [typeId, visibleTypes]);
  const selectedType = visibleTypes.find((x) => x.id === typeId);
  const choose = async (item: MenuItemDto) => {
    try {
      const groups =
        item.kind === "SingleProduct"
          ? await api.get<ModifierGroupDto[]>(
              `/api/modifiers?menuItemId=${item.id}`,
            )
          : [];
      const slots =
        item.kind === "Combo"
          ? await api.get<ComboComponentDto[]>(
              `/api/restaurant-catalog/combos/${item.id}`,
            )
          : [];
      if (!groups.length && !slots.length) {
        setCart((x) => [
          ...x,
          {
            item,
            quantity: 1,
            modifierOptionIds: [],
            comboSelections: [],
            delta: 0,
          },
        ]);
        return;
      }
      setConfig({
        item,
        groups,
        slots,
        selected: slots.flatMap((slot) =>
          slot.options
            .filter((option) => option.isDefault)
            .map((option) => option.id),
        ),
      });
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : t("common.saveError"));
    }
  };
  const confirm = () => {
    if (!config) return;
    for (const g of config.groups) {
      const n = g.options.filter((o) => config.selected.includes(o.id)).length,
        min = g.isRequired ? Math.max(1, g.minSelect) : g.minSelect;
      if (n < min || n > g.maxSelect) {
        toast.error(`${name(g)}: ${min}-${g.maxSelect}`);
        return;
      }
    }
    for (const s of config.slots) {
      const n = s.options.filter((o) => config.selected.includes(o.id)).length,
        min = s.isRequired ? Math.max(1, s.minSelect) : s.minSelect;
      if (n < min || n > s.maxSelect) {
        toast.error(`${s.slotLabel}: ${min}-${s.maxSelect}`);
        return;
      }
    }
    const mods = config.groups
      .flatMap((g) => g.options)
      .filter((o) => config.selected.includes(o.id));
    const combos = config.slots.flatMap((s) =>
      s.options
        .filter((o) => config.selected.includes(o.id))
        .map((o) => ({
          comboComponentId: s.id,
          selectedMenuItemId: o.menuItemId,
        })),
    );
    setCart((x) => [
      ...x,
      {
        item: config.item,
        quantity: 1,
        modifierOptionIds: mods.map((x) => x.id),
        comboSelections: combos,
        delta:
          mods.reduce((a, b) => a + b.priceDelta, 0) +
          config.slots
            .flatMap((s) => s.options)
            .filter((o) => config.selected.includes(o.id))
            .reduce((a, b) => a + b.priceDelta, 0),
      },
    ]);
    setConfig(null);
  };
  const submit = async () => {
    try {
      if (selectedType?.code === "DINE_IN" && !tableId) {
        toast.error(t("restaurantOrders.tableRequired"));
        return;
      }
      const order = await api.post<RestaurantOrderDto>(
        "/api/restaurant-orders",
        {
          branchId,
          orderTypeId: typeId,
          tableId: selectedType?.code === "DINE_IN" ? tableId : null,
          carPlateNumber: selectedType?.code === "CAR_PICKUP" ? carPlate : null,
          discountAmount: 0,
          lines: cart.map((x) => ({
            menuItemId: x.item.id,
            quantity: x.quantity,
            notes: null,
            modifierOptionIds: x.modifierOptionIds,
            comboSelections: x.comboSelections,
          })),
        },
      );
      setCart([]);
      setCarPlate("");
      setOrders((x) => [order, ...x]);
      toast.success(t("restaurantOrders.created"));
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : t("common.saveError"));
    }
  };
  const total = cart.reduce(
    (a, x) => a + (x.item.basePrice + x.delta) * x.quantity,
    0,
  );
  const print = async (id: string) => {
    try {
      await api.post(`/api/restaurant-orders/${id}/confirm`, {});
      setOrders((x) =>
        x.map((o) => (o.id === id ? { ...o, status: "Sent" } : o)),
      );
      toast.success(t("restaurantOrders.printed"));
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : t("common.saveError"));
    }
  };
  const approve = async (id: string) => {
    try {
      const updated = await api.post<RestaurantOrderDto>(
        `/api/restaurant-orders/${id}/approve-qr`,
        {},
      );
      setOrders((x) => x.map((o) => (o.id === id ? updated : o)));
      toast.success(t("restaurantOrders.approved"));
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : t("common.saveError"));
    }
  };
  const reject = async (id: string) => {
    const reason = window.prompt(t("restaurantOrders.rejectionReason"));
    if (!reason) return;
    try {
      const updated = await api.post<RestaurantOrderDto>(
        `/api/restaurant-orders/${id}/reject-qr`,
        { reason },
      );
      setOrders((x) => x.map((o) => (o.id === id ? updated : o)));
      toast.success(t("restaurantOrders.rejected"));
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : t("common.saveError"));
    }
  };
  const editPending = async (order: RestaurantOrderDto) => {
    const activeItems = order.items.filter((item) => !item.isCancelled);
    const quantities: number[] = [];
    for (const item of activeItems) {
      const value = window.prompt(
        `${t("restaurantOrders.quantityFor")} ${item.name}`,
        String(item.quantity),
      );
      if (value === null) return;
      const quantity = Number(value);
      if (!Number.isInteger(quantity) || quantity < 1 || quantity > 50) {
        toast.error(t("restaurantOrders.invalidQuantity"));
        return;
      }
      quantities.push(quantity);
    }
    try {
      const updated = await api.put<RestaurantOrderDto>(
        `/api/restaurant-orders/${order.id}/pending-qr`,
        {
          lines: activeItems.map((item, index) => ({
            menuItemId: item.menuItemId,
            quantity: quantities[index],
            notes: item.notes,
            modifierOptionIds: item.modifierOptionIds,
            comboSelections: item.comboSelections.map((selection) => ({
              comboComponentId: selection.comboComponentId,
              selectedMenuItemId: selection.optionId,
            })),
          })),
        },
      );
      setOrders((current) =>
        current.map((item) => (item.id === order.id ? updated : item)),
      );
      toast.success(t("restaurantOrders.edited"));
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : t("common.saveError"));
    }
  };
  return (
    <section>
      <h1>{t("restaurantOrders.title")}</h1>
      <div className="table-toolbar">
        <select value={branchId} onChange={(e) => setBranchId(e.target.value)}>
          {branches.map((x) => (
            <option key={x.id} value={x.id}>
              {name(x)}
            </option>
          ))}
        </select>
        <select value={typeId} onChange={(e) => setTypeId(e.target.value)}>
          {visibleTypes.map((x) => (
            <option key={x.id} value={x.id}>
              {name(x)}
            </option>
          ))}
        </select>
        {selectedType?.code === "DINE_IN" && (
          <select value={tableId} onChange={(e) => setTableId(e.target.value)}>
            <option value="">{t("restaurantOrders.selectTable")}</option>
            {tables.map((x) => (
              <option key={x.id} value={x.id}>
                {x.label}
              </option>
            ))}
          </select>
        )}
        {selectedType?.code === "CAR_PICKUP" && (
          <input
            value={carPlate}
            onChange={(e) => setCarPlate(e.target.value)}
            placeholder={t("restaurantOrders.carPlate")}
          />
        )}
      </div>
      <div className="grid gap-6 lg:grid-cols-[1fr_22rem]">
        <div className="cashier-products-grid">
          {visible.map((x) => (
            <button
              className="product-card p-4"
              key={x.id}
              onClick={() => choose(x)}
            >
              <strong>{name(x)}</strong>
              <Money value={x.basePrice} />
            </button>
          ))}
        </div>
        <aside className="settings-card">
          <h2>{t("cashier.cart")}</h2>
          {cart.map((x, i) => (
            <div className="table-toolbar" key={i}>
              <span>{name(x.item)}</span>
              <Money value={(x.item.basePrice + x.delta) * x.quantity} />
              <button
                onClick={() => setCart((c) => c.filter((_, n) => n !== i))}
              >
                ×
              </button>
            </div>
          ))}
          <strong>
            <Money value={total} />
          </strong>
          <button disabled={!cart.length} onClick={submit}>
            {t("restaurantOrders.create")}
          </button>
        </aside>
      </div>
      <h2>{t("restaurantOrders.recent")}</h2>
      {orders.slice(0, 10).map((x) => (
        <div className="table-toolbar" key={x.id}>
          <span>
            #{x.orderNumber} · {x.orderTypeCode} · {x.status}
            {x.tableLabel ? ` · ${x.tableLabel}` : ""}
          </span>
          <Money value={x.grandTotal} />
          {x.status === "Open" && !x.salesChannelCode?.startsWith("QR_") && (
            <button onClick={() => void print(x.id)}>
              {t("restaurantOrders.confirmPrint")}
            </button>
          )}
          {x.status === "PendingApproval" && (
            <>
              <button onClick={() => void approve(x.id)}>
                {t("restaurantOrders.approve")}
              </button>
              <button
                className="button-secondary"
                onClick={() => void editPending(x)}
              >
                {t("common.edit")}
              </button>
              <button
                className="button-danger"
                onClick={() => void reject(x.id)}
              >
                {t("restaurantOrders.reject")}
              </button>
            </>
          )}
        </div>
      ))}
      {config && (
        <div className="app-scrim fixed inset-0 z-50 flex items-center justify-center">
          <div className="settings-card max-h-[85vh] w-[min(42rem,94vw)] overflow-auto">
            <h2>{name(config.item)}</h2>
            {config.groups.map((g) => (
              <fieldset key={g.id}>
                <legend>
                  {name(g)} (
                  {g.isRequired
                    ? t("restaurant.required")
                    : t("modifiers.minimum")}
                  )
                </legend>
                {g.options
                  .filter((o) => o.isActive)
                  .map((o) => (
                    <label className="checkbox-row" key={o.id}>
                      <input
                        type={g.maxSelect === 1 ? "radio" : "checkbox"}
                        name={g.id}
                        checked={config.selected.includes(o.id)}
                        onChange={() =>
                          setConfig(
                            (c) =>
                              c && {
                                ...c,
                                selected:
                                  g.maxSelect === 1
                                    ? [
                                        ...c.selected.filter(
                                          (id) =>
                                            !g.options.some((o) => o.id === id),
                                        ),
                                        o.id,
                                      ]
                                    : c.selected.includes(o.id)
                                      ? c.selected.filter((id) => id !== o.id)
                                      : [...c.selected, o.id],
                              },
                          )
                        }
                      />
                      {name(o)} <Money value={o.priceDelta} />
                    </label>
                  ))}
              </fieldset>
            ))}
            {config.slots.map((s) => (
              <fieldset key={s.id}>
                <legend>{s.slotLabel}</legend>
                {s.options.map((o) => (
                  <label className="checkbox-row" key={o.id}>
                    <input
                      type={s.maxSelect === 1 ? "radio" : "checkbox"}
                      name={s.id}
                      checked={config.selected.includes(o.id)}
                      onChange={() =>
                        setConfig(
                          (c) =>
                            c && {
                              ...c,
                              selected:
                                s.maxSelect === 1
                                  ? [
                                      ...c.selected.filter(
                                        (id) =>
                                          !s.options.some((o) => o.id === id),
                                      ),
                                      o.id,
                                    ]
                                  : c.selected.includes(o.id)
                                    ? c.selected.filter((id) => id !== o.id)
                                    : [...c.selected, o.id],
                            },
                        )
                      }
                    />
                    {name({
                      nameAr: o.menuItemNameAr,
                      nameEn: o.menuItemNameEn,
                    })}{" "}
                    <Money value={o.priceDelta} />
                  </label>
                ))}
              </fieldset>
            ))}
            <div className="modal-actions">
              <button onClick={() => setConfig(null)}>
                {t("common.cancel")}
              </button>
              <button onClick={confirm}>{t("orders.add")}</button>
            </div>
          </div>
        </div>
      )}
    </section>
  );
}
