// Allows CSS custom properties such as "--gap" in inline styles (the CSS uses them for spacing).
import "react";

declare module "react" {
  interface CSSProperties {
    [key: `--${string}`]: string | number | undefined;
  }
}
