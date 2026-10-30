using Microsoft.AspNetCore.Mvc;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews(o => { o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true; o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()); });
builder.Services.AddDistributedMemoryCache(); builder.Services.AddSession(o => { o.IdleTimeout = TimeSpan.FromMinutes(30); o.Cookie.HttpOnly = true; o.Cookie.IsEssential = true; o.Cookie.SameSite = SameSiteMode.Strict; });
builder.Services.AddHttpClient("api", c => c.BaseAddress = new Uri(builder.Configuration["ApiUrl"] ?? "http://localhost:5281"));
var app = builder.Build(); if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }
app.UseStaticFiles(); app.UseRouting(); app.UseSession(); app.MapControllerRoute("default", "{controller=Portal}/{action=Index}/{id?}"); app.Run();
