/**
 * Cliente tipado para el backend SteamMarket.Api.
 * Todas las llamadas van a rutas relativas ("/api/...") porque en dev
 * Astro las proxea hacia el backend (ver astro.config.mjs) y en produccion
 * se espera que el backend quede detras del mismo dominio/reverse proxy.
 *
 * credentials: "include" es clave: el login es por cookie de sesion,
 * sin esto el navegador no la manda ni la guarda en las respuestas.
 */

export interface MeResponse {
  authenticated: boolean;
  steamId?: string;
  name?: string;
  isAdmin?: boolean;
  isSuperAdmin?: boolean;
}

export interface InventoryItemDto {
  assetId: string;
  name: string;
  marketHashName: string;
  type: string;
  tradable: boolean;
  marketable: boolean;
  iconUrl: string | null;
  marketPrice: number | null;
  payoutPrice: number | null;
  rarity: string | null;
  quality: string | null;
  rarityColor: string | null;
  hero: string | null;
}

export interface InventoryResponse {
  success: boolean;
  error?: string | null;
  items: InventoryItemDto[];
  count: number;
  fetchedAtUtc?: string | null;
}

export interface ProfileResponse {
  steamId: string;
  name: string;
  tradeUrl: string | null;
  firstName: string | null;
  lastName: string | null;
  email: string | null;
  phone: string | null;
  documentType: string | null;
  documentNumber: string | null;
}

export interface PersonalDataInput {
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  documentType: string;
  documentNumber: string;
}

export interface SellOrderItemDto {
  assetId: string;
  name: string;
  iconUrl: string | null;
  payoutPrice: number;
}

export interface SellOrderDto {
  id: string;
  steamId: string;
  items: SellOrderItemDto[];
  totalAmount: number;
  status: "Pending" | "Completed" | "Rejected";
  createdAtUtc: string;
  resolvedAtUtc: string | null;
  adminNote: string | null;
}

export interface CreateSellOrderResponse {
  success: boolean;
  error?: string;
  order?: SellOrderDto;
  adminTradeUrl?: string;
  /** true si el bot ya te mando la oferta de intercambio a tu cuenta de Steam (solo falta aceptarla). */
  botOfferSent?: boolean;
  /** Si botOfferSent es false, por que no se pudo mandar sola (bot desconectado, falta Trade URL, etc). */
  botOfferError?: string;
}

export interface WalletTransactionDto {
  id: string;
  amount: number;
  type: string;
  description: string | null;
  createdAtUtc: string;
}

export interface WalletResponse {
  balance: number;
  transactions: WalletTransactionDto[];
}

export interface WithdrawalDto {
  id: string;
  steamId: string;
  amount: number;
  method: string;
  destination: string;
  status: "Pending" | "Paid" | "Rejected";
  createdAtUtc: string;
  resolvedAtUtc: string | null;
}

export interface AdminAccountDto {
  steamId: string;
  tradeUrl: string;
  label: string | null;
  isSuperAdmin: boolean;
  addedAtUtc: string | null;
}

// --- Cajas (loot boxes) ---

export interface LootBoxDto {
  id: string;
  slug: string;
  name: string;
  category: string;
  price: number;
  maxItemPrice: number | null;
  imageUrl: string | null;
  sortOrder: number;
  isActive: boolean;
}

export interface WarehouseItemDto {
  marketHashName: string;
  displayName: string;
  hero: string | null;
  type: string;
  rarity: string;
  imageUrl: string | null;
  quantity: number;
  price: number | null;
}

export interface LootBoxPoolItemDto {
  id: string;
  marketHashName: string;
  displayName: string;
  hero: string | null;
  slot: string | null;
  type: string;
  rarity: string;
  imageUrl: string | null;
  weight: number;
}

export interface LootBoxDetailDto {
  box: LootBoxDto;
  contents: LootBoxPoolItemDto[];
  pityCount: number;
}

export interface LootBoxWinDto {
  id: string;
  boxName: string;
  itemName: string;
  itemImageUrl: string | null;
  rarity: string;
  status: "Reserved" | "PendingRedeem" | "Sold" | "Redeemed";
  wonAtUtc: string;
  resolvedAtUtc: string | null;
}

export interface LootBoxOpenResponse {
  success: boolean;
  error?: string;
  win?: LootBoxWinDto;
  wasFree?: boolean;
  pityCount?: number;
}

export interface LootBoxDemoResponse {
  success: boolean;
  error?: string;
  item?: LootBoxPoolItemDto;
}

export const PITY_THRESHOLD = 9;

const jsonHeaders = { Accept: "application/json" };

/** URL a la que hay que mandar al navegador (no es fetch, es un redirect completo). */
export function loginUrl(returnUrl: string): string {
  return `/api/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`;
}

export async function getMe(): Promise<MeResponse> {
  const res = await fetch("/api/auth/me", {
    credentials: "include",
    headers: jsonHeaders,
  });

  if (!res.ok) return { authenticated: false };
  return (await res.json()) as MeResponse;
}

export async function logout(): Promise<void> {
  await fetch("/api/auth/logout", {
    method: "POST",
    credentials: "include",
    headers: jsonHeaders,
  });
}

/**
 * force = true ignora el cache del backend y fuerza un pedido en vivo a Steam.
 * Usar solo cuando el usuario lo pide explicitamente (boton "Actualizar"), no en cada carga
 * de pagina: el endpoint de inventario de Steam rate-limita agresivo por IP.
 */
export async function getDotaInventory(force = false): Promise<InventoryResponse> {
  const url = force ? "/api/inventory/dota?force=true" : "/api/inventory/dota";
  const res = await fetch(url, {
    credentials: "include",
    headers: jsonHeaders,
  });

  if (!res.ok) {
    // 401 (sin sesion) y 502 (Steam fallo / inventario privado) devuelven { error: string }.
    const body = await res.json().catch(() => null);
    return {
      success: false,
      error: body?.error ?? `El servidor respondio ${res.status}.`,
      items: [],
      count: 0,
    };
  }

  return (await res.json()) as InventoryResponse;
}

export async function getProfile(): Promise<ProfileResponse | null> {
  const res = await fetch("/api/profile", { credentials: "include", headers: jsonHeaders });
  if (!res.ok) return null;
  return (await res.json()) as ProfileResponse;
}

/**
 * Extrae un mensaje de error legible de una respuesta no-ok: errores de validacion
 * (FluentValidation -> ValidationProblem, campo "errors"), o el "detail" que manda
 * GlobalExceptionHandler en errores 500 no controlados (en dev trae la excepcion real).
 */
async function extractError(res: Response): Promise<string> {
  const body = await res.json().catch(() => null);
  const firstError = body?.errors ? Object.values(body.errors)[0] : null;
  const message = Array.isArray(firstError) ? firstError[0] : null;
  return message ?? body?.error ?? body?.detail ?? `El servidor respondio ${res.status}.`;
}

/** Devuelve null si se guardo bien, o un mensaje de error (formato invalido, sin sesion, etc). */
export async function updateTradeUrl(tradeUrl: string): Promise<string | null> {
  const res = await fetch("/api/profile/trade-url", {
    method: "PUT",
    credentials: "include",
    headers: { ...jsonHeaders, "Content-Type": "application/json" },
    body: JSON.stringify({ tradeUrl }),
  });

  return res.ok ? null : await extractError(res);
}

/** Devuelve null si se guardo bien, o un mensaje de error. */
export async function savePersonalData(data: PersonalDataInput): Promise<string | null> {
  const res = await fetch("/api/profile/personal-data", {
    method: "PUT",
    credentials: "include",
    headers: { ...jsonHeaders, "Content-Type": "application/json" },
    body: JSON.stringify(data),
  });

  return res.ok ? null : await extractError(res);
}

/** Borra los datos personales guardados (conserva la Trade URL). Devuelve null si salio bien. */
export async function deletePersonalData(): Promise<string | null> {
  const res = await fetch("/api/profile/personal-data", {
    method: "DELETE",
    credentials: "include",
    headers: jsonHeaders,
  });

  return res.ok ? null : await extractError(res);
}

/** Confirma la venta de los items seleccionados (por assetId). El backend re-cotiza server-side. */
export async function createSellOrder(assetIds: string[]): Promise<CreateSellOrderResponse> {
  const res = await fetch("/api/orders", {
    method: "POST",
    credentials: "include",
    headers: { ...jsonHeaders, "Content-Type": "application/json" },
    body: JSON.stringify({ assetIds }),
  });

  if (!res.ok) return { success: false, error: await extractError(res) };
  return (await res.json()) as CreateSellOrderResponse;
}

export async function getMyOrders(): Promise<SellOrderDto[]> {
  const res = await fetch("/api/orders", { credentials: "include", headers: jsonHeaders });
  if (!res.ok) return [];
  return (await res.json()) as SellOrderDto[];
}

export async function getWallet(): Promise<WalletResponse | null> {
  const res = await fetch("/api/wallet", { credentials: "include", headers: jsonHeaders });
  if (!res.ok) return null;
  return (await res.json()) as WalletResponse;
}

export async function requestWithdrawal(
  amount: number,
  method: string,
  destination: string
): Promise<{ error: string | null }> {
  const res = await fetch("/api/wallet/withdrawals", {
    method: "POST",
    credentials: "include",
    headers: { ...jsonHeaders, "Content-Type": "application/json" },
    body: JSON.stringify({ amount, method, destination }),
  });

  return { error: res.ok ? null : await extractError(res) };
}

export async function getMyWithdrawals(): Promise<WithdrawalDto[]> {
  const res = await fetch("/api/wallet/withdrawals", { credentials: "include", headers: jsonHeaders });
  if (!res.ok) return [];
  return (await res.json()) as WithdrawalDto[];
}

export async function getLootBoxes(): Promise<LootBoxDto[]> {
  const res = await fetch("/api/lootboxes", { headers: jsonHeaders });
  if (!res.ok) return [];
  return (await res.json()) as LootBoxDto[];
}

export async function getLootBoxDetail(slug: string): Promise<LootBoxDetailDto | null> {
  const res = await fetch(`/api/lootboxes/${encodeURIComponent(slug)}`, {
    credentials: "include",
    headers: jsonHeaders,
  });
  if (!res.ok) return null;
  return (await res.json()) as LootBoxDetailDto;
}

export async function demoOpenLootBox(slug: string): Promise<LootBoxDemoResponse> {
  const res = await fetch(`/api/lootboxes/${encodeURIComponent(slug)}/demo`, {
    method: "POST",
    credentials: "include",
    headers: jsonHeaders,
  });
  if (!res.ok) return { success: false, error: await extractError(res) };
  return (await res.json()) as LootBoxDemoResponse;
}

export async function openLootBox(slug: string): Promise<LootBoxOpenResponse> {
  const res = await fetch(`/api/lootboxes/${encodeURIComponent(slug)}/open`, {
    method: "POST",
    credentials: "include",
    headers: jsonHeaders,
  });
  if (!res.ok) return { success: false, error: await extractError(res) };
  return (await res.json()) as LootBoxOpenResponse;
}

export async function getMyLootBoxWins(): Promise<LootBoxWinDto[]> {
  const res = await fetch("/api/lootboxes/me/wins", { credentials: "include", headers: jsonHeaders });
  if (!res.ok) return [];
  return (await res.json()) as LootBoxWinDto[];
}

export async function sellLootBoxWin(id: string): Promise<{ error: string | null; creditedAmount?: number }> {
  const res = await fetch(`/api/lootboxes/wins/${id}/sell`, {
    method: "POST",
    credentials: "include",
    headers: jsonHeaders,
  });
  if (!res.ok) return { error: await extractError(res) };
  const body = await res.json();
  return { error: null, creditedAmount: body.creditedAmount };
}

export async function redeemLootBoxWin(id: string): Promise<string | null> {
  const res = await fetch(`/api/lootboxes/wins/${id}/redeem`, {
    method: "POST",
    credentials: "include",
    headers: jsonHeaders,
  });
  return res.ok ? null : await extractError(res);
}

// --- Admin ---

export async function getPendingOrders(): Promise<SellOrderDto[]> {
  const res = await fetch("/api/admin/orders/pending", { credentials: "include", headers: jsonHeaders });
  if (!res.ok) return [];
  return (await res.json()) as SellOrderDto[];
}

export async function completeOrder(id: string): Promise<string | null> {
  const res = await fetch(`/api/admin/orders/${id}/complete`, {
    method: "POST",
    credentials: "include",
    headers: jsonHeaders,
  });
  return res.ok ? null : await extractError(res);
}

export async function rejectOrder(id: string, note?: string): Promise<string | null> {
  const res = await fetch(`/api/admin/orders/${id}/reject`, {
    method: "POST",
    credentials: "include",
    headers: { ...jsonHeaders, "Content-Type": "application/json" },
    body: JSON.stringify({ note: note ?? null }),
  });
  return res.ok ? null : await extractError(res);
}

export async function getPendingWithdrawals(): Promise<WithdrawalDto[]> {
  const res = await fetch("/api/admin/withdrawals/pending", { credentials: "include", headers: jsonHeaders });
  if (!res.ok) return [];
  return (await res.json()) as WithdrawalDto[];
}

export async function markWithdrawalPaid(id: string): Promise<string | null> {
  const res = await fetch(`/api/admin/withdrawals/${id}/paid`, {
    method: "POST",
    credentials: "include",
    headers: jsonHeaders,
  });
  return res.ok ? null : await extractError(res);
}

export async function rejectWithdrawal(id: string): Promise<string | null> {
  const res = await fetch(`/api/admin/withdrawals/${id}/reject`, {
    method: "POST",
    credentials: "include",
    headers: jsonHeaders,
  });
  return res.ok ? null : await extractError(res);
}

// --- Admin: catalogo de cajas y almacen ---

export async function getAdminLootBoxes(): Promise<LootBoxDto[]> {
  const res = await fetch("/api/admin/lootboxes", { credentials: "include", headers: jsonHeaders });
  if (!res.ok) return [];
  return (await res.json()) as LootBoxDto[];
}

export async function saveLootBox(input: {
  slug: string;
  name: string;
  category: string;
  price: number;
  maxItemPrice: number | null;
  imageUrl: string | null;
  sortOrder: number;
  isActive: boolean;
}): Promise<string | null> {
  const res = await fetch("/api/admin/lootboxes", {
    method: "POST",
    credentials: "include",
    headers: { ...jsonHeaders, "Content-Type": "application/json" },
    body: JSON.stringify(input),
  });
  return res.ok ? null : await extractError(res);
}

export async function getAdminLootBoxItems(slug: string): Promise<LootBoxPoolItemDto[]> {
  const res = await fetch(`/api/admin/lootboxes/${encodeURIComponent(slug)}/items`, {
    credentials: "include",
    headers: jsonHeaders,
  });
  if (!res.ok) return [];
  return (await res.json()) as LootBoxPoolItemDto[];
}

export async function removeLootBoxItem(itemId: string): Promise<string | null> {
  const res = await fetch(`/api/admin/lootboxes/items/${itemId}`, {
    method: "DELETE",
    credentials: "include",
    headers: jsonHeaders,
  });
  return res.ok ? null : await extractError(res);
}

export async function getWarehouse(): Promise<WarehouseItemDto[]> {
  const res = await fetch("/api/admin/warehouse", { credentials: "include", headers: jsonHeaders });
  if (!res.ok) return [];
  return (await res.json()) as WarehouseItemDto[];
}

export async function addLootBoxItemFromStock(slug: string, marketHashName: string): Promise<string | null> {
  const res = await fetch(`/api/admin/lootboxes/${encodeURIComponent(slug)}/items/from-stock`, {
    method: "POST",
    credentials: "include",
    headers: { ...jsonHeaders, "Content-Type": "application/json" },
    body: JSON.stringify({ marketHashName }),
  });
  return res.ok ? null : await extractError(res);
}

// --- Gestión de cuentas admin (solo super admin) ---

export async function getAdminAccounts(): Promise<AdminAccountDto[]> {
  const res = await fetch("/api/admin/accounts", { credentials: "include", headers: jsonHeaders });
  if (!res.ok) return [];
  return (await res.json()) as AdminAccountDto[];
}

export async function addAdminAccount(steamId64: string, tradeUrl: string, label: string): Promise<string | null> {
  const res = await fetch("/api/admin/accounts", {
    method: "POST",
    credentials: "include",
    headers: { ...jsonHeaders, "Content-Type": "application/json" },
    body: JSON.stringify({ steamId64, tradeUrl, label: label || null }),
  });
  return res.ok ? null : await extractError(res);
}

export async function removeAdminAccount(steamId64: string): Promise<string | null> {
  const res = await fetch(`/api/admin/accounts/${steamId64}`, {
    method: "DELETE",
    credentials: "include",
    headers: jsonHeaders,
  });
  return res.ok ? null : await extractError(res);
}
