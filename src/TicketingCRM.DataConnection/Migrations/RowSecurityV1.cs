namespace TicketingCRM.DataConnection.Migrations;

/// <summary>Frozen V1 row policies. New tables require a new migration and policy revision.</summary>
internal static class RowSecurityV1
{
    internal const string PostgreSqlUp = """
DO $setup$
BEGIN
 IF NOT EXISTS(SELECT FROM pg_roles WHERE rolname='appsouls_runtime') THEN
  CREATE ROLE appsouls_runtime NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;
  COMMENT ON ROLE appsouls_runtime IS 'App Souls restricted runtime v1';
 ELSIF (SELECT shobj_description(oid,'pg_authid') FROM pg_roles WHERE rolname='appsouls_runtime') IS DISTINCT FROM 'App Souls restricted runtime v1'
 OR EXISTS(SELECT FROM pg_roles WHERE rolname='appsouls_runtime' AND (rolsuper OR rolbypassrls OR rolcreaterole OR rolcreatedb OR rolcanlogin OR rolreplication)) THEN
  RAISE EXCEPTION 'The appsouls_runtime role is not an approved restricted App Souls principal';
 END IF;
 EXECUTE format('GRANT appsouls_runtime TO %I',current_user);
END $setup$;
GRANT USAGE ON SCHEMA public TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "AspNetRoles" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "AspNetRoleClaims" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "AspNetUserClaims" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "AspNetUserLogins" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "AspNetUserRoles" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "AspNetUserTokens" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "AspNetUsers" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "CollectionForms" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "CommerceOrder" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "Contracts" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "Customers" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "CustomerActivities" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "CustomerInvitations" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "Entitlements" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "FormSubmissions" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "Installations" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "InvoiceDocument" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "LicenseRevisions" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "LicenseSchemaRevisions" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "OutboxMessages" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "Products" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "ProductPricing" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "ProductReleases" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "SalesHistories" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "SalesLeads" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "SalesOpportunities" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "SupportAttachment" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "SupportChat" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "SupportChatMessage" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "SupportReply" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "SupportTickets" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "SupportTicketHistories" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "Tenants" TO appsouls_runtime;
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "TenantMemberships" TO appsouls_runtime;
ALTER TABLE "CollectionForms" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "CollectionForms" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "CollectionForms" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL) OR ("Id"::text=NULLIF(current_setting('appsouls.public_form_id',true),''))))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL) OR ("Id"::text=NULLIF(current_setting('appsouls.public_form_id',true),'')))));
ALTER TABLE "CommerceOrder" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "CommerceOrder" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "CommerceOrder" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false))));
ALTER TABLE "Contracts" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Contracts" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "Contracts" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false))));
ALTER TABLE "Customers" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Customers" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "Customers" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "Id"::text=current_setting('appsouls.customer_id',true))) OR (false)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "Id"::text=current_setting('appsouls.customer_id',true))) OR (false))));
ALTER TABLE "CustomerActivities" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "CustomerActivities" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "CustomerActivities" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false))));
ALTER TABLE "CustomerInvitations" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "CustomerInvitations" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "CustomerInvitations" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL) OR (false)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL) OR (false))));
ALTER TABLE "Entitlements" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Entitlements" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "Entitlements" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false))));
ALTER TABLE "FormSubmissions" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "FormSubmissions" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "FormSubmissions" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL) OR ("FormId"::text=NULLIF(current_setting('appsouls.public_form_id',true),''))))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL) OR ("FormId"::text=NULLIF(current_setting('appsouls.public_form_id',true),'')))));
ALTER TABLE "Installations" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Installations" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "Installations" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false))));
ALTER TABLE "InvoiceDocument" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "InvoiceDocument" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "InvoiceDocument" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false))));
ALTER TABLE "LicenseRevisions" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "LicenseRevisions" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "LicenseRevisions" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false))));
ALTER TABLE "LicenseSchemaRevisions" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "LicenseSchemaRevisions" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "LicenseSchemaRevisions" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL) OR (false)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL) OR (false))));
ALTER TABLE "OutboxMessages" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "OutboxMessages" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "OutboxMessages" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL) OR (false)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL) OR (false))));
ALTER TABLE "Products" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Products" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "Products" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL) OR (false)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL) OR (false))));
ALTER TABLE "ProductPricing" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "ProductPricing" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "ProductPricing" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL) OR (false)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL) OR (false))));
ALTER TABLE "ProductReleases" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "ProductReleases" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "ProductReleases" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL) OR (false)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL) OR (false))));
ALTER TABLE "SalesHistories" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "SalesHistories" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "SalesHistories" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL) OR (false)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL) OR (false))));
ALTER TABLE "SalesLeads" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "SalesLeads" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "SalesLeads" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL) OR (false)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL) OR (false))));
ALTER TABLE "SalesOpportunities" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "SalesOpportunities" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "SalesOpportunities" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL) OR (false)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL) OR (false))));
ALTER TABLE "SupportAttachment" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "SupportAttachment" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "SupportAttachment" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false))));
ALTER TABLE "SupportChat" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "SupportChat" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "SupportChat" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false))));
ALTER TABLE "SupportChatMessage" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "SupportChatMessage" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "SupportChatMessage" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false))));
ALTER TABLE "SupportReply" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "SupportReply" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "SupportReply" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false))));
ALTER TABLE "SupportTickets" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "SupportTickets" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "SupportTickets" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false))));
ALTER TABLE "SupportTicketHistories" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "SupportTicketHistories" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "SupportTicketHistories" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL) OR (false)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL) OR (false))));
ALTER TABLE "Tenants" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Tenants" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "Tenants" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL) OR (NULLIF(current_setting('appsouls.public_form_id',true),'') IS NOT NULL)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL) OR (NULLIF(current_setting('appsouls.public_form_id',true),'') IS NOT NULL))));
ALTER TABLE "TenantMemberships" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "TenantMemberships" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "TenantMemberships" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false))));

""";
    internal const string PostgreSqlDown = """
DROP POLICY appsouls_scope_v1 ON "CollectionForms";
ALTER TABLE "CollectionForms" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "CollectionForms" NO FORCE ROW LEVEL SECURITY;
DROP POLICY appsouls_scope_v1 ON "CommerceOrder";
ALTER TABLE "CommerceOrder" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "CommerceOrder" NO FORCE ROW LEVEL SECURITY;
DROP POLICY appsouls_scope_v1 ON "Contracts";
ALTER TABLE "Contracts" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "Contracts" NO FORCE ROW LEVEL SECURITY;
DROP POLICY appsouls_scope_v1 ON "Customers";
ALTER TABLE "Customers" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "Customers" NO FORCE ROW LEVEL SECURITY;
DROP POLICY appsouls_scope_v1 ON "CustomerActivities";
ALTER TABLE "CustomerActivities" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "CustomerActivities" NO FORCE ROW LEVEL SECURITY;
DROP POLICY appsouls_scope_v1 ON "CustomerInvitations";
ALTER TABLE "CustomerInvitations" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "CustomerInvitations" NO FORCE ROW LEVEL SECURITY;
DROP POLICY appsouls_scope_v1 ON "Entitlements";
ALTER TABLE "Entitlements" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "Entitlements" NO FORCE ROW LEVEL SECURITY;
DROP POLICY appsouls_scope_v1 ON "FormSubmissions";
ALTER TABLE "FormSubmissions" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "FormSubmissions" NO FORCE ROW LEVEL SECURITY;
DROP POLICY appsouls_scope_v1 ON "Installations";
ALTER TABLE "Installations" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "Installations" NO FORCE ROW LEVEL SECURITY;
DROP POLICY appsouls_scope_v1 ON "InvoiceDocument";
ALTER TABLE "InvoiceDocument" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "InvoiceDocument" NO FORCE ROW LEVEL SECURITY;
DROP POLICY appsouls_scope_v1 ON "LicenseRevisions";
ALTER TABLE "LicenseRevisions" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "LicenseRevisions" NO FORCE ROW LEVEL SECURITY;
DROP POLICY appsouls_scope_v1 ON "LicenseSchemaRevisions";
ALTER TABLE "LicenseSchemaRevisions" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "LicenseSchemaRevisions" NO FORCE ROW LEVEL SECURITY;
DROP POLICY appsouls_scope_v1 ON "OutboxMessages";
ALTER TABLE "OutboxMessages" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "OutboxMessages" NO FORCE ROW LEVEL SECURITY;
DROP POLICY appsouls_scope_v1 ON "Products";
ALTER TABLE "Products" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "Products" NO FORCE ROW LEVEL SECURITY;
DROP POLICY appsouls_scope_v1 ON "ProductPricing";
ALTER TABLE "ProductPricing" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "ProductPricing" NO FORCE ROW LEVEL SECURITY;
DROP POLICY appsouls_scope_v1 ON "ProductReleases";
ALTER TABLE "ProductReleases" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "ProductReleases" NO FORCE ROW LEVEL SECURITY;
DROP POLICY appsouls_scope_v1 ON "SalesHistories";
ALTER TABLE "SalesHistories" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "SalesHistories" NO FORCE ROW LEVEL SECURITY;
DROP POLICY appsouls_scope_v1 ON "SalesLeads";
ALTER TABLE "SalesLeads" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "SalesLeads" NO FORCE ROW LEVEL SECURITY;
DROP POLICY appsouls_scope_v1 ON "SalesOpportunities";
ALTER TABLE "SalesOpportunities" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "SalesOpportunities" NO FORCE ROW LEVEL SECURITY;
DROP POLICY appsouls_scope_v1 ON "SupportAttachment";
ALTER TABLE "SupportAttachment" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "SupportAttachment" NO FORCE ROW LEVEL SECURITY;
DROP POLICY appsouls_scope_v1 ON "SupportChat";
ALTER TABLE "SupportChat" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "SupportChat" NO FORCE ROW LEVEL SECURITY;
DROP POLICY appsouls_scope_v1 ON "SupportChatMessage";
ALTER TABLE "SupportChatMessage" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "SupportChatMessage" NO FORCE ROW LEVEL SECURITY;
DROP POLICY appsouls_scope_v1 ON "SupportReply";
ALTER TABLE "SupportReply" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "SupportReply" NO FORCE ROW LEVEL SECURITY;
DROP POLICY appsouls_scope_v1 ON "SupportTickets";
ALTER TABLE "SupportTickets" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "SupportTickets" NO FORCE ROW LEVEL SECURITY;
DROP POLICY appsouls_scope_v1 ON "SupportTicketHistories";
ALTER TABLE "SupportTicketHistories" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "SupportTicketHistories" NO FORCE ROW LEVEL SECURITY;
DROP POLICY appsouls_scope_v1 ON "Tenants";
ALTER TABLE "Tenants" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "Tenants" NO FORCE ROW LEVEL SECURITY;
DROP POLICY appsouls_scope_v1 ON "TenantMemberships";
ALTER TABLE "TenantMemberships" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "TenantMemberships" NO FORCE ROW LEVEL SECURITY;

""";
    internal const string SqlServerPrincipal = """
IF DATABASE_PRINCIPAL_ID(N'appsouls_runtime') IS NULL
BEGIN
 CREATE USER [appsouls_runtime] WITHOUT LOGIN;
 EXEC sys.sp_addextendedproperty @name=N'AppSoulsManaged',@value=N'Runtime v1',@level0type=N'USER',@level0name=N'appsouls_runtime';
END
ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=4 AND major_id=DATABASE_PRINCIPAL_ID(N'appsouls_runtime') AND name=N'AppSoulsManaged' AND CONVERT(nvarchar(100),value)=N'Runtime v1')
 THROW 51000,'The appsouls_runtime principal is not managed by App Souls',1;
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[AspNetRoles] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[AspNetRoleClaims] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[AspNetUserClaims] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[AspNetUserLogins] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[AspNetUserRoles] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[AspNetUserTokens] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[AspNetUsers] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[CollectionForms] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[CommerceOrder] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[Contracts] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[Customers] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[CustomerActivities] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[CustomerInvitations] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[Entitlements] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[FormSubmissions] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[Installations] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[InvoiceDocument] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[LicenseRevisions] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[LicenseSchemaRevisions] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[OutboxMessages] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[Products] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[ProductPricing] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[ProductReleases] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[SalesHistories] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[SalesLeads] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[SalesOpportunities] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[SupportAttachment] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[SupportChat] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[SupportChatMessage] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[SupportReply] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[SupportTickets] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[SupportTicketHistories] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[Tenants] TO [appsouls_runtime];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[TenantMemberships] TO [appsouls_runtime];

""";
    internal const string SqlServerFunction = """
CREATE FUNCTION dbo.AppSoulsRowScopeV1(@tenant uniqueidentifier,@customer uniqueidentifier,@form uniqueidentifier,@publicMode int,@staffOnly bit)
RETURNS TABLE WITH SCHEMABINDING AS RETURN
SELECT 1 AS permitted WHERE USER_NAME()<>N'appsouls_runtime' OR
(@tenant=TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'appsouls_tenant')) AND
 ((TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'appsouls_public_form')) IS NOT NULL AND
    (@publicMode=1 OR (@publicMode=2 AND @form=TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'appsouls_public_form')))))
  OR (TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'appsouls_public_form')) IS NULL AND
    (TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'appsouls_customer')) IS NULL OR
     (@staffOnly=0 AND (@customer IS NULL OR @customer=TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'appsouls_customer'))))))));

""";
    internal const string SqlServerPolicies = """
CREATE SECURITY POLICY dbo.[AppSouls_CollectionForms_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,[Id],2,1) ON dbo.[CollectionForms], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,[Id],2,1) ON dbo.[CollectionForms] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,[Id],2,1) ON dbo.[CollectionForms] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
CREATE SECURITY POLICY dbo.[AppSouls_CommerceOrder_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[CommerceOrder], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[CommerceOrder] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[CommerceOrder] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
CREATE SECURITY POLICY dbo.[AppSouls_Contracts_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[Contracts], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[Contracts] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[Contracts] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
CREATE SECURITY POLICY dbo.[AppSouls_Customers_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[Id],NULL,0,0) ON dbo.[Customers], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[Id],NULL,0,0) ON dbo.[Customers] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[Id],NULL,0,0) ON dbo.[Customers] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
CREATE SECURITY POLICY dbo.[AppSouls_CustomerActivities_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[CustomerActivities], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[CustomerActivities] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[CustomerActivities] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
CREATE SECURITY POLICY dbo.[AppSouls_CustomerInvitations_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,1) ON dbo.[CustomerInvitations], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,1) ON dbo.[CustomerInvitations] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,1) ON dbo.[CustomerInvitations] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
CREATE SECURITY POLICY dbo.[AppSouls_Entitlements_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[Entitlements], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[Entitlements] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[Entitlements] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
CREATE SECURITY POLICY dbo.[AppSouls_FormSubmissions_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,[FormId],2,1) ON dbo.[FormSubmissions], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,[FormId],2,1) ON dbo.[FormSubmissions] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,[FormId],2,1) ON dbo.[FormSubmissions] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
CREATE SECURITY POLICY dbo.[AppSouls_Installations_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[Installations], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[Installations] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[Installations] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
CREATE SECURITY POLICY dbo.[AppSouls_InvoiceDocument_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[InvoiceDocument], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[InvoiceDocument] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[InvoiceDocument] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
CREATE SECURITY POLICY dbo.[AppSouls_LicenseRevisions_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[LicenseRevisions], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[LicenseRevisions] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[LicenseRevisions] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
CREATE SECURITY POLICY dbo.[AppSouls_LicenseSchemaRevisions_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,0) ON dbo.[LicenseSchemaRevisions], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,0) ON dbo.[LicenseSchemaRevisions] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,0) ON dbo.[LicenseSchemaRevisions] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
CREATE SECURITY POLICY dbo.[AppSouls_OutboxMessages_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,0) ON dbo.[OutboxMessages], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,0) ON dbo.[OutboxMessages] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,0) ON dbo.[OutboxMessages] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
CREATE SECURITY POLICY dbo.[AppSouls_Products_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,0) ON dbo.[Products], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,0) ON dbo.[Products] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,0) ON dbo.[Products] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
CREATE SECURITY POLICY dbo.[AppSouls_ProductPricing_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,1) ON dbo.[ProductPricing], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,1) ON dbo.[ProductPricing] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,1) ON dbo.[ProductPricing] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
CREATE SECURITY POLICY dbo.[AppSouls_ProductReleases_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,0) ON dbo.[ProductReleases], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,0) ON dbo.[ProductReleases] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,0) ON dbo.[ProductReleases] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
CREATE SECURITY POLICY dbo.[AppSouls_SalesHistories_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,1) ON dbo.[SalesHistories], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,1) ON dbo.[SalesHistories] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,1) ON dbo.[SalesHistories] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
CREATE SECURITY POLICY dbo.[AppSouls_SalesLeads_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,1) ON dbo.[SalesLeads], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,1) ON dbo.[SalesLeads] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,1) ON dbo.[SalesLeads] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
CREATE SECURITY POLICY dbo.[AppSouls_SalesOpportunities_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,1) ON dbo.[SalesOpportunities], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,1) ON dbo.[SalesOpportunities] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,1) ON dbo.[SalesOpportunities] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
CREATE SECURITY POLICY dbo.[AppSouls_SupportAttachment_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[SupportAttachment], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[SupportAttachment] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[SupportAttachment] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
CREATE SECURITY POLICY dbo.[AppSouls_SupportChat_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[SupportChat], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[SupportChat] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[SupportChat] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
CREATE SECURITY POLICY dbo.[AppSouls_SupportChatMessage_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[SupportChatMessage], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[SupportChatMessage] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[SupportChatMessage] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
CREATE SECURITY POLICY dbo.[AppSouls_SupportReply_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[SupportReply], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[SupportReply] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[SupportReply] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
CREATE SECURITY POLICY dbo.[AppSouls_SupportTickets_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[SupportTickets], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[SupportTickets] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[SupportTickets] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
CREATE SECURITY POLICY dbo.[AppSouls_SupportTicketHistories_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,0) ON dbo.[SupportTicketHistories], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,0) ON dbo.[SupportTicketHistories] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,0,0) ON dbo.[SupportTicketHistories] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
CREATE SECURITY POLICY dbo.[AppSouls_Tenants_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,1,0) ON dbo.[Tenants], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,1,0) ON dbo.[Tenants] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],NULL,NULL,1,0) ON dbo.[Tenants] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
CREATE SECURITY POLICY dbo.[AppSouls_TenantMemberships_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[TenantMemberships], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[TenantMemberships] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[TenantMemberships] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);

""";
    internal const string SqlServerDown = """
DROP SECURITY POLICY dbo.[AppSouls_CollectionForms_V1];
DROP SECURITY POLICY dbo.[AppSouls_CommerceOrder_V1];
DROP SECURITY POLICY dbo.[AppSouls_Contracts_V1];
DROP SECURITY POLICY dbo.[AppSouls_Customers_V1];
DROP SECURITY POLICY dbo.[AppSouls_CustomerActivities_V1];
DROP SECURITY POLICY dbo.[AppSouls_CustomerInvitations_V1];
DROP SECURITY POLICY dbo.[AppSouls_Entitlements_V1];
DROP SECURITY POLICY dbo.[AppSouls_FormSubmissions_V1];
DROP SECURITY POLICY dbo.[AppSouls_Installations_V1];
DROP SECURITY POLICY dbo.[AppSouls_InvoiceDocument_V1];
DROP SECURITY POLICY dbo.[AppSouls_LicenseRevisions_V1];
DROP SECURITY POLICY dbo.[AppSouls_LicenseSchemaRevisions_V1];
DROP SECURITY POLICY dbo.[AppSouls_OutboxMessages_V1];
DROP SECURITY POLICY dbo.[AppSouls_Products_V1];
DROP SECURITY POLICY dbo.[AppSouls_ProductPricing_V1];
DROP SECURITY POLICY dbo.[AppSouls_ProductReleases_V1];
DROP SECURITY POLICY dbo.[AppSouls_SalesHistories_V1];
DROP SECURITY POLICY dbo.[AppSouls_SalesLeads_V1];
DROP SECURITY POLICY dbo.[AppSouls_SalesOpportunities_V1];
DROP SECURITY POLICY dbo.[AppSouls_SupportAttachment_V1];
DROP SECURITY POLICY dbo.[AppSouls_SupportChat_V1];
DROP SECURITY POLICY dbo.[AppSouls_SupportChatMessage_V1];
DROP SECURITY POLICY dbo.[AppSouls_SupportReply_V1];
DROP SECURITY POLICY dbo.[AppSouls_SupportTickets_V1];
DROP SECURITY POLICY dbo.[AppSouls_SupportTicketHistories_V1];
DROP SECURITY POLICY dbo.[AppSouls_Tenants_V1];
DROP SECURITY POLICY dbo.[AppSouls_TenantMemberships_V1];
DROP FUNCTION dbo.AppSoulsRowScopeV1;

""";
}
