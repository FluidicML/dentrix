# Dentrix Code Signing

Integrating with Dentrix is painful. The only way to do it, even in a
development environment, is to create a signed Windows application. Signing
requires a certificate that:

> must contain a public key with RSA encryption of at least RSA 2048 Bits and
> it must be issued by a CA or a Trusted Authority that is approved by
> Microsoft. (Digicert, SSL.com, GlobalSign, Sectigo (Comodo), etc...)

We ended up using DigiCert and its unlikely we'll ever switch away considering
how tedious the organization validation is (especially EV validation we opted
into). When our certificate expires, we'll need to either renew it or create a
new one. In most cases we probably just want to renew and not think about it
any further.

To finish registering with Dentrix, sign the `DtxSqlBrowser.exe` file included
in this directory and send that to the Dentrix team. Funny enough, the only
explanation that actually details this is the following excerpt from their
"documentation":

> Note: Please be aware of reported changes with obtaining a certificate from a
> CA Authority. Please prepare for that take more time than before. Nothing has
> changed on our end as the expectation is still the same to sign the
> dtxsqlbrowser and send that to us zipped to be added to our servers.

## Code Signing Certificate Creation

### Azure

Navigate to the `kv-dentrix-shared-eastus` Key Vault in Azure. In the sidebar,
click on "Objects > Certificates". Afterwards, press "Generate/Import". Fill
out the following:

Field | Value
----- | -----
Method of Certificate Creation | Generate
Type of Certificate Authority (CA) | Certificate issued by a non-integrated CA
Subject | CN=fluidicml.com, O="Fluidic ML, INC.", L=Fremont, S=California, C=US
Content Type | PKCS #12

Next, click "Not configured" under "Advanced Policy Configuration" and enter
the following details:

Field | Value
----- | -----
Extended Key Usages (EKUs) | `1.3.6.1.5.5.7.3.1, 1.3.6.1.5.5.7.3.2, 1.3.6.1.5.5.7.3.3, 1.3.6.1.4.1.311.10.3.13
`
X.509 Key Usage Flags | Digital Signature, Key Encipherment
Exportable Private Key? | No
Key Type | RSA-HSM
Key Size | 3072
Enable Certificate Transparency? | No
Certificate Type | *Leave Blank*

For any other specified form fields, it is up to you what to choose. Just make
sure the values align with what DigiCert also asks.

---

Once finished, click on the newly created entry in the table. Press
"Certificate Operation" and download the CSR. With this in hand, you can now go
to DigiCert.

### DigiCert

Once logged in, select "Certificates > Orders" in the sidebar. On the new page,
press "Request a certificate > EV Code Signing". Fill out the following:

Field | Value
----- | -----
Validity Period | *Matches value in Azure*
Organization | Fluidic ML, INC.
Provisioning options | Install on an HSM
FIPS 140-2 level 2 HSM | Yes

You can then upload the CSR downloaded from Azure. Once the order has been
made, the configured recipient will get an email to approve. The certificate
should be issued shortly after.

### Back to Azure

Unzip the emailed bundle of certificates. Go back to the created certificate
entry and click "Certificate Operations". This time you'll click "Merge Signed
Request". Specify the organization specific certificate to finish the process.
